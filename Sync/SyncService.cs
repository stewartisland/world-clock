using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Microsoft.Identity.Client;

namespace WorldClock.Sync;

public enum SyncState { NotConfigured, SignedOut, Syncing, Synced, Offline, SignInAgain, UpdateApp }

/// <summary>
/// Keeps clocks.json in step with the copy in the user's OneDrive app folder.
///
/// Local changes are uploaded 2 seconds after the last edit. The saved copy is checked on startup, after sign-in,
/// when the window is focused (at most once a minute) and on "Sync now". Uploads use If-Match on the last-seen
/// eTag, so a change from another PC is never overwritten blindly: on a conflict the newer copy (by updatedAt)
/// wins, and the copy it replaces is kept in clocks.backup.json.
/// </summary>
public sealed class SyncService
{
    private static readonly string DefaultStateDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldClock");

    private static readonly TimeSpan FocusCheckInterval = TimeSpan.FromSeconds(60);

    private readonly IAccountService _auth;
    private readonly ISettingsStore _store;
    private readonly string _statePath;
    private readonly SettingsService _settingsService;
    private readonly Func<AppSettings> _getLocal;
    private readonly Action<AppSettings> _applyRemote;
    private readonly Func<AppSettings, AppSettings, bool?> _askUseSaved;
    private readonly DispatcherTimer _uploadDebounce = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _retry = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LocalSyncState _state;
    private DateTime _lastCheckUtc;

    /// <param name="getLocal">Returns the current settings.</param>
    /// <param name="applyRemote">Replaces the current settings with a synced copy.</param>
    /// <param name="askUseSaved">First sign-in with different clocks: true = use saved, false = keep this PC's,
    /// null = cancel sign-in. Arguments are (this PC's, saved).</param>
    /// <param name="stateDirectory">Where sync.json is kept; defaults to %LOCALAPPDATA%\WorldClock.</param>
    public SyncService(IAccountService auth, ISettingsStore store, SettingsService settingsService,
        Func<AppSettings> getLocal, Action<AppSettings> applyRemote, Func<AppSettings, AppSettings, bool?> askUseSaved,
        string? stateDirectory = null)
    {
        _auth = auth;
        _store = store;
        _statePath = Path.Combine(stateDirectory ?? DefaultStateDirectory, "sync.json");
        _state = LoadState();
        _settingsService = settingsService;
        _getLocal = getLocal;
        _applyRemote = applyRemote;
        _askUseSaved = askUseSaved;
        State = auth.IsConfigured ? SyncState.SignedOut : SyncState.NotConfigured;

        _uploadDebounce.Tick += async (_, _) => { _uploadDebounce.Stop(); await SyncAsync(); };
        _retry.Tick += async (_, _) =>
        {
            if (State is SyncState.Offline || (_state.PendingUpload && IsSignedIn))
                await SyncAsync();
        };
    }

    public SyncState State { get; private set; }
    public DateTime? LastSyncedUtc => _state.LastSyncedUtc;
    public string? AccountName => _auth.AccountName;
    public bool IsConfigured => _auth.IsConfigured;
    public bool IsSignedIn => State is not (SyncState.NotConfigured or SyncState.SignedOut);

    public event Action? StateChanged;

    /// <summary>On startup: restores a previous sign-in and syncs.</summary>
    public async Task StartAsync()
    {
        if (!await _auth.RestoreAsync()) return;
        _retry.Start();
        await SyncAsync();
    }

    /// <summary>Shows the account picker, then does the first sync (asking which clocks to keep if they differ).</summary>
    public async Task SignInAsync(IntPtr window)
    {
        try
        {
            if (!await _auth.SignInAsync(window)) return;
        }
        catch (MsalException)
        {
            SetState(SyncState.SignedOut);
            throw;
        }

        _state = new LocalSyncState();
        SaveState();
        _retry.Start();
        await SyncAsync(firstSignIn: true);
    }

    /// <summary>Signs out. This PC keeps its clocks; the saved copy stays in OneDrive.</summary>
    public async Task SignOutAsync()
    {
        _uploadDebounce.Stop();
        _retry.Stop();
        await _auth.SignOutAsync();
        _state = new LocalSyncState();
        SaveState();
        SetState(SyncState.SignedOut);
    }

    /// <summary>Call after every local change; uploads once edits pause.</summary>
    public void LocalChanged()
    {
        if (!IsSignedIn) return;
        _state.PendingUpload = true;
        SaveState();
        _uploadDebounce.Stop();
        _uploadDebounce.Start();
    }

    /// <summary>Call when the window is activated; checks for changes from other PCs at most once a minute.</summary>
    public async Task WindowActivatedAsync()
    {
        if (IsSignedIn && DateTime.UtcNow - _lastCheckUtc > FocusCheckInterval)
            await SyncAsync();
    }

    public Task SyncNowAsync() => SyncAsync();

    private async Task SyncAsync(bool firstSignIn = false)
    {
        if (!_auth.IsConfigured || !await _gate.WaitAsync(0)) return;
        try
        {
            _lastCheckUtc = DateTime.UtcNow;
            SetState(SyncState.Syncing);
            // One retry covers a conflict caused by another PC saving between our download and upload.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    SetState(await ReconcileAsync(firstSignIn));
                    return;
                }
                catch (SyncConflictException) when (attempt == 0)
                {
                }
            }
            SetState(SyncState.Offline);
        }
        catch (MsalUiRequiredException)
        {
            SetState(SyncState.SignInAgain);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                          or MsalServiceException or MsalClientException or SyncConflictException)
        {
            SetState(SyncState.Offline);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SyncState> ReconcileAsync(bool firstSignIn)
    {
        var local = _getLocal();
        var remote = await _store.DownloadAsync();

        // Nothing saved yet: this PC's clocks become the saved copy.
        if (remote is null)
            return await UploadAsync(local, ifMatch: null);

        var saved = SettingsService.Deserialize(remote.Json);
        if (saved.Version > AppSettings.CurrentVersion)
            return SyncState.UpdateApp;

        if (firstSignIn)
            return await FirstSyncAsync(local, saved, remote.ETag);

        // No change on the other side since we last synced.
        if (remote.ETag == _state.ETag)
            return _state.PendingUpload ? await UploadAsync(local, remote.ETag) : Synced(remote.ETag);

        // Both may have changed: the most recent edit wins, and the other copy is kept as a backup.
        if (saved.UpdatedAt > local.UpdatedAt)
        {
            if (_state.PendingUpload)
                _settingsService.SaveBackup(local);
            _applyRemote(saved);
            return Synced(remote.ETag);
        }
        return await UploadAsync(local, remote.ETag);
    }

    private async Task<SyncState> FirstSyncAsync(AppSettings local, AppSettings saved, string eTag)
    {
        if (SameClocks(local, saved) || SettingsService.IsDefaultClocks(local.Clocks))
        {
            _applyRemote(saved);
            return Synced(eTag);
        }

        switch (_askUseSaved(local, saved))
        {
            case true:
                _settingsService.SaveBackup(local);
                _applyRemote(saved);
                return Synced(eTag);
            case false:
                _settingsService.SaveBackup(saved);
                return await UploadAsync(local, eTag);
            default:
                await SignOutAsync();
                return SyncState.SignedOut;
        }
    }

    private async Task<SyncState> UploadAsync(AppSettings local, string? ifMatch)
    {
        var eTag = await _store.UploadAsync(SettingsService.Serialize(local), ifMatch);
        return Synced(eTag);
    }

    private SyncState Synced(string eTag)
    {
        _state.ETag = eTag;
        _state.PendingUpload = false;
        _state.LastSyncedUtc = DateTime.UtcNow;
        SaveState();
        return SyncState.Synced;
    }

    private static bool SameClocks(AppSettings a, AppSettings b) => a.Clocks.SequenceEqual(b.Clocks);

    private void SetState(SyncState state)
    {
        State = state;
        StateChanged?.Invoke();
    }

    // %LOCALAPPDATA%\WorldClock\sync.json: the last-seen eTag, and whether there are local changes to upload.

    private sealed class LocalSyncState
    {
        [JsonPropertyName("eTag")] public string? ETag { get; set; }
        [JsonPropertyName("pendingUpload")] public bool PendingUpload { get; set; }
        [JsonPropertyName("lastSyncedUtc")] public DateTime? LastSyncedUtc { get; set; }
    }

    private LocalSyncState LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
                return JsonSerializer.Deserialize<LocalSyncState>(File.ReadAllText(_statePath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
        }
        return new();
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statePath)!);
            File.WriteAllText(_statePath, JsonSerializer.Serialize(_state));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
