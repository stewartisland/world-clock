using System.Windows;
using System.Windows.Interop;
using Microsoft.Identity.Client;
using WorldClock.Sync;

namespace WorldClock;

/// <summary>Microsoft sign-in and OneDrive sync: the ⋯ menu's Sync section and the status text.</summary>
public partial class MainWindow
{
    private SyncService _sync = null!;

    /// <summary>True while synced settings are being applied, so they aren't treated as a local change.</summary>
    private bool _applyingRemote;

    private void InitializeSync()
    {
        var account = new MicrosoftAuth();
        _sync = new SyncService(account, new OneDriveStore(account), _settingsService,
            () => _settings, ApplyRemoteSettings, AskUseSaved);
        _sync.StateChanged += ShowSyncStatus;
        Activated += async (_, _) => await _sync.WindowActivatedAsync();
    }

    /// <summary>Replaces this PC's clocks, unit and theme with the synced copy. Always on top stays per-PC.</summary>
    private void ApplyRemoteSettings(AppSettings saved)
    {
        _applyingRemote = true;
        try
        {
            _clocks.Clear();
            foreach (var config in saved.Clocks)
            {
                if (CityClock.TryCreate(config, out var clock))
                    _clocks.Add(clock);
            }
            _settings.Clocks = _clocks.Select(c => c.ToConfig()).ToList();
            _settings.TemperatureUnit = saved.TemperatureUnit;
            _settings.Theme = saved.Theme;
            _settings.UpdatedAt = saved.UpdatedAt;
            _settings.DeviceName = saved.DeviceName;
            _settings.LocationLookupDone = true;
            ThemeManager.SetPreference(_settings.Theme);
            _settingsService.Save(_settings, isLocalChange: false);
        }
        finally
        {
            _applyingRemote = false;
        }
        Refresh();
        _ = RefreshWeatherAsync();
    }

    private bool? AskUseSaved(AppSettings local, AppSettings saved)
    {
        var dialog = new SyncChoiceWindow(local, saved) { Owner = this };
        return dialog.ShowDialog() == true ? dialog.UseSaved : null;
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _sync.SignInAsync(new WindowInteropHelper(this).Handle);
        }
        catch (MsalException ex)
        {
            MessageBox.Show(this, $"Couldn't sign in to your Microsoft account.\n\n{ex.Message}", "World Clock",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e) => await _sync.SyncNowAsync();

    private async void SignOut_Click(object sender, RoutedEventArgs e) => await _sync.SignOutAsync();

    private void UpdateSyncMenu()
    {
        var signedIn = _sync.IsSignedIn;
        AccountItem.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        AccountItem.Header = _sync.AccountName;
        SignInItem.Visibility = signedIn && _sync.State != SyncState.SignInAgain ? Visibility.Collapsed : Visibility.Visible;
        SignInItem.Header = _sync.State == SyncState.SignInAgain ? "Sign in again…" : "Sign in with Microsoft…";
        SignInItem.IsEnabled = _sync.IsConfigured;
        SignInItem.ToolTip = _sync.IsConfigured ? null : "Sync isn't set up in this build of World Clock";
        SyncNowItem.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
        SignOutItem.Visibility = signedIn ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowSyncStatus()
    {
        SyncStatusText.Text = _sync.State switch
        {
            SyncState.Syncing => "Syncing…",
            SyncState.Synced => "Synced " + Ago(_sync.LastSyncedUtc),
            SyncState.Offline => "Offline, changes will sync",
            SyncState.SignInAgain => "Sign in again to keep syncing (⋯ menu)",
            SyncState.UpdateApp => "Update World Clock to sync",
            _ => "",
        };
        SyncStatusText.ToolTip = _sync.IsSignedIn ? $"Syncing with {_sync.AccountName}'s OneDrive" : null;
    }

    private static string Ago(DateTime? utc)
    {
        if (utc is not { } at) return "";
        var minutes = (int)(DateTime.UtcNow - at).TotalMinutes;
        return minutes switch
        {
            < 1 => "just now",
            1 => "1 min ago",
            < 60 => $"{minutes} min ago",
            _ => $"at {at.ToLocalTime():h:mm tt}",
        };
    }
}
