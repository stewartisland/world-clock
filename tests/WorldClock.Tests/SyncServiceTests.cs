using System.IO;
using System.Net.Http;
using WorldClock.Sync;

namespace WorldClock.Tests;

/// <summary>
/// Sync decisions against an in-memory OneDrive. The rule under test throughout: a change is never lost.
/// Whatever gets replaced is either the older copy or is kept in clocks.backup.json.
/// </summary>
public sealed class SyncServiceTests : IDisposable
{
    private static readonly DateTimeOffset Earlier = new(2026, 9, 23, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Earlier.AddHours(1);

    private readonly string _dir = Directory.CreateTempSubdirectory("worldclock-tests-").FullName;
    private readonly FakeStore _store = new();
    private readonly FakeAccount _account = new();
    private readonly SettingsService _settingsService;
    private AppSettings _local;
    private readonly List<AppSettings> _applied = [];
    private bool? _answer;
    private int _asked;

    public SyncServiceTests()
    {
        _settingsService = new SettingsService(_dir);
        _local = Settings(Earlier, "Tokyo", "Paris");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SyncService CreateService() => new(_account, _store, _settingsService,
        () => _local,
        saved => { _applied.Add(saved); _local = saved; },
        (_, _) => { _asked++; return _answer; },
        stateDirectory: _dir);

    private static AppSettings Settings(DateTimeOffset updatedAt, params string[] cities) => new()
    {
        UpdatedAt = updatedAt,
        Clocks = cities.Select(c => new ClockConfig(c, "UTC")).ToList(),
    };

    private string? BackupCities()
    {
        var path = Path.Combine(_dir, "clocks.backup.json");
        return File.Exists(path)
            ? string.Join(",", SettingsService.Deserialize(File.ReadAllText(path)).Clocks.Select(c => c.City))
            : null;
    }

    private static string Cities(AppSettings s) => string.Join(",", s.Clocks.Select(c => c.City));

    // First sign-in

    [Fact]
    public async Task FirstSignIn_NothingSaved_UploadsThisPc()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        Assert.Equal(SyncState.Synced, sync.State);
        var upload = Assert.Single(_store.Uploads);
        Assert.Null(upload.IfMatch); // create-only, so it can't overwrite a file another PC just made
        Assert.Equal("Tokyo,Paris", Cities(_store.Saved!));
        Assert.Equal(0, _asked);
    }

    [Fact]
    public async Task FirstSignIn_ThisPcHasDefaults_UsesSavedWithoutAsking()
    {
        _local = new AppSettings { UpdatedAt = Later, Clocks = [.. SettingsService.Defaults] };
        _store.Put(Settings(Earlier, "Oslo"));
        var sync = CreateService();

        await sync.SignInAsync(IntPtr.Zero);

        Assert.Equal(0, _asked);
        Assert.Equal("Oslo", Cities(_local));
        Assert.Empty(_store.Uploads);
    }

    [Fact]
    public async Task FirstSignIn_Different_UseSaved_BacksUpThisPc()
    {
        _store.Put(Settings(Earlier, "Oslo"));
        _answer = true;
        var sync = CreateService();

        await sync.SignInAsync(IntPtr.Zero);

        Assert.Equal(1, _asked);
        Assert.Equal("Oslo", Cities(_local));
        Assert.Equal("Tokyo,Paris", BackupCities());
        Assert.Empty(_store.Uploads);
    }

    [Fact]
    public async Task FirstSignIn_Different_KeepThisPc_UploadsOverSavedAndBacksItUp()
    {
        _store.Put(Settings(Later, "Oslo"));
        var savedETag = _store.ETag;
        _answer = false;
        var sync = CreateService();

        await sync.SignInAsync(IntPtr.Zero);

        Assert.Equal("Tokyo,Paris", Cities(_store.Saved!));
        Assert.Equal(savedETag, Assert.Single(_store.Uploads).IfMatch);
        Assert.Equal("Oslo", BackupCities());
        Assert.Empty(_applied);
    }

    [Fact]
    public async Task FirstSignIn_Different_Cancel_SignsOutAndChangesNothing()
    {
        _store.Put(Settings(Later, "Oslo"));
        _answer = null;
        var sync = CreateService();

        await sync.SignInAsync(IntPtr.Zero);

        Assert.Equal(SyncState.SignedOut, sync.State);
        Assert.True(_account.SignedOut);
        Assert.Empty(_applied);
        Assert.Empty(_store.Uploads);
        Assert.Null(BackupCities());
    }

    // Ongoing sync

    [Fact]
    public async Task LocalChange_SavedUnchanged_UploadsWithIfMatch()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);
        var eTag = _store.ETag;

        _local = Settings(Later, "Tokyo", "Paris", "Lima");
        sync.LocalChanged();
        await sync.SyncNowAsync();

        Assert.Equal("Tokyo,Paris,Lima", Cities(_store.Saved!));
        Assert.Equal(eTag, _store.Uploads[^1].IfMatch);
    }

    [Fact]
    public async Task NoChangesAnywhere_DoesNotUpload()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        await sync.SyncNowAsync();

        Assert.Single(_store.Uploads); // only the first sign-in upload
        Assert.Equal(SyncState.Synced, sync.State);
    }

    [Fact]
    public async Task OtherPcSavedNewer_AppliesIt()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        _store.Put(Settings(Later, "Oslo")); // another PC saves
        await sync.SyncNowAsync();

        Assert.Equal("Oslo", Cities(_local));
        Assert.Null(BackupCities()); // nothing unsynced on this PC, so nothing to back up
    }

    [Fact]
    public async Task BothChanged_OtherPcNewer_AppliesItAndBacksUpThisPc()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        _local = Settings(Earlier.AddMinutes(30), "Tokyo", "Paris", "Lima"); // this PC, offline edit
        sync.LocalChanged();
        _store.Put(Settings(Later, "Oslo"));                                // other PC, later edit
        await sync.SyncNowAsync();

        Assert.Equal("Oslo", Cities(_local));
        Assert.Equal("Tokyo,Paris,Lima", BackupCities());
    }

    [Fact]
    public async Task BothChanged_ThisPcNewer_UploadsOverIt()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        _store.Put(Settings(Earlier.AddMinutes(30), "Oslo"));
        var otherETag = _store.ETag;
        _local = Settings(Later, "Tokyo", "Paris", "Lima");
        sync.LocalChanged();
        await sync.SyncNowAsync();

        Assert.Equal("Tokyo,Paris,Lima", Cities(_store.Saved!));
        Assert.Equal(otherETag, _store.Uploads[^1].IfMatch);
        Assert.Empty(_applied.Where(a => Cities(a) == "Oslo"));
    }

    [Fact]
    public async Task UploadConflict_RereadsAndResolves()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        _local = Settings(Earlier.AddMinutes(10), "Tokyo");
        sync.LocalChanged();
        // Another PC saves a newer copy between our download and our upload.
        _store.BeforeNextUpload = () => _store.Put(Settings(Later, "Oslo"));
        await sync.SyncNowAsync();

        Assert.Equal(SyncState.Synced, sync.State);
        Assert.Equal("Oslo", Cities(_local));
        Assert.Equal("Oslo", Cities(_store.Saved!));
        Assert.Equal("Tokyo", BackupCities());
    }

    [Fact]
    public async Task SavedByNewerAppVersion_IsNeverOverwritten()
    {
        var newer = Settings(Later, "Oslo");
        newer.Version = AppSettings.CurrentVersion + 1;
        _store.Put(newer);
        var sync = CreateService();

        await sync.SignInAsync(IntPtr.Zero);

        Assert.Equal(SyncState.UpdateApp, sync.State);
        Assert.Empty(_store.Uploads);
        Assert.Empty(_applied);
    }

    [Fact]
    public async Task Offline_KeepsChangeQueued_AndUploadsWhenBack()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);

        _local = Settings(Later, "Lima");
        sync.LocalChanged();
        _store.Offline = true;
        await sync.SyncNowAsync();
        Assert.Equal(SyncState.Offline, sync.State);

        _store.Offline = false;
        await sync.SyncNowAsync();
        Assert.Equal(SyncState.Synced, sync.State);
        Assert.Equal("Lima", Cities(_store.Saved!));
    }

    [Fact]
    public async Task QueuedChange_SurvivesRestart()
    {
        var sync = CreateService();
        await sync.SignInAsync(IntPtr.Zero);
        _local = Settings(Later, "Lima");
        sync.LocalChanged();

        // App closes before uploading; a new instance restores the sign-in and syncs.
        var restarted = CreateService();
        await restarted.StartAsync();

        Assert.Equal("Lima", Cities(_store.Saved!));
    }

    private sealed class FakeAccount : IAccountService
    {
        public bool SignedOut { get; private set; }
        public bool IsConfigured => true;
        public string? AccountName => "test@outlook.com";
        public Task<bool> RestoreAsync() => Task.FromResult(true);
        public Task<bool> SignInAsync(IntPtr window) => Task.FromResult(true);
        public Task SignOutAsync() { SignedOut = true; return Task.CompletedTask; }
    }

    /// <summary>In-memory OneDrive file with eTag checks like Graph's If-Match / conflictBehavior=fail.</summary>
    private sealed class FakeStore : ISettingsStore
    {
        private int _version;
        public string? Json { get; private set; }
        public string? ETag { get; private set; }
        public AppSettings? Saved => Json is null ? null : SettingsService.Deserialize(Json);
        public List<(string Json, string? IfMatch)> Uploads { get; } = [];
        public bool Offline { get; set; }
        public Action? BeforeNextUpload { get; set; }

        public void Put(AppSettings settings)
        {
            Json = SettingsService.Serialize(settings);
            ETag = $"\"etag-{++_version}\"";
        }

        public Task<RemoteFile?> DownloadAsync(CancellationToken ct = default)
        {
            if (Offline) throw new HttpRequestException("offline");
            return Task.FromResult(Json is null ? null : new RemoteFile(Json, ETag!));
        }

        public Task<string> UploadAsync(string json, string? ifMatchETag, CancellationToken ct = default)
        {
            if (Offline) throw new HttpRequestException("offline");
            var before = BeforeNextUpload;
            BeforeNextUpload = null;
            before?.Invoke();

            if (ifMatchETag is null ? Json is not null : ifMatchETag != ETag)
                throw new SyncConflictException();

            Uploads.Add((json, ifMatchETag));
            Json = json;
            ETag = $"\"etag-{++_version}\"";
            return Task.FromResult(ETag);
        }
    }
}
