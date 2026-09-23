namespace WorldClock.Sync;

/// <summary>The signed-in account. Implemented by <see cref="MicrosoftAuth"/>; a Google version would slot in here.</summary>
public interface IAccountService
{
    bool IsConfigured { get; }
    string? AccountName { get; }
    Task<bool> RestoreAsync();
    Task<bool> SignInAsync(IntPtr window);
    Task SignOutAsync();
}

/// <summary>Where the synced settings file lives. Implemented by <see cref="OneDriveStore"/>.</summary>
public interface ISettingsStore
{
    /// <summary>The saved file, or null if nothing has been saved yet.</summary>
    Task<RemoteFile?> DownloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the file only if its eTag still matches <paramref name="ifMatchETag"/> (or, when null, only if it
    /// doesn't exist yet), and returns the new eTag. Throws <see cref="SyncConflictException"/> otherwise.
    /// </summary>
    Task<string> UploadAsync(string json, string? ifMatchETag, CancellationToken ct = default);
}
