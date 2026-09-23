using System.IO;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using Microsoft.Identity.Client.Extensions.Msal;

namespace WorldClock.Sync;

/// <summary>
/// Sign-in with a personal Microsoft account through the Windows account broker (WAM), using MSAL.
/// Requests only access to the app's own OneDrive folder.
/// </summary>
public sealed class MicrosoftAuth : IAccountService
{
    /// <summary>
    /// Application (client) ID of the World Clock app registration. A public client ID, so it's safe to commit.
    /// Forks should register their own app (see docs/developer-guide.md#microsoft-sign-in) and either change this
    /// or set the WORLDCLOCK_MS_CLIENT_ID environment variable.
    /// </summary>
    private const string ClientId = "";

    private static readonly string[] Scopes = ["Files.ReadWrite.AppFolder"];

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorldClock");

    private readonly IPublicClientApplication? _app;
    private Task? _cacheReady;

    public MicrosoftAuth()
    {
        var clientId = Environment.GetEnvironmentVariable("WORLDCLOCK_MS_CLIENT_ID") is { Length: > 0 } fromEnv
            ? fromEnv
            : ClientId;
        if (clientId.Length == 0) return;

        _app = PublicClientApplicationBuilder.Create(clientId)
            .WithAuthority(AadAuthorityAudience.PersonalMicrosoftAccount)
            .WithDefaultRedirectUri()
            .WithBroker(new BrokerOptions(BrokerOptions.OperatingSystems.Windows) { Title = "World Clock" })
            .Build();
    }

    /// <summary>False when no client ID is configured in this build; sign-in is then unavailable.</summary>
    public bool IsConfigured => _app is not null;

    /// <summary>The signed-in account's email, if any.</summary>
    public string? AccountName { get; private set; }

    /// <summary>Restores a previous sign-in from the encrypted token cache. Returns false if there's none.</summary>
    public async Task<bool> RestoreAsync()
    {
        if (_app is null) return false;
        await EnsureCacheAsync();
        var account = (await _app.GetAccountsAsync()).FirstOrDefault();
        AccountName = account?.Username;
        return account is not null;
    }

    /// <summary>Shows the Windows account picker. Returns false if the user cancels.</summary>
    public async Task<bool> SignInAsync(IntPtr window)
    {
        if (_app is null) return false;
        await EnsureCacheAsync();
        try
        {
            var result = await _app.AcquireTokenInteractive(Scopes)
                .WithParentActivityOrWindow(window)
                .WithPrompt(Prompt.SelectAccount)
                .ExecuteAsync();
            AccountName = result.Account.Username;
            return true;
        }
        catch (MsalClientException ex) when (ex.ErrorCode == MsalError.AuthenticationCanceledError)
        {
            return false;
        }
    }

    public async Task SignOutAsync()
    {
        if (_app is null) return;
        await EnsureCacheAsync();
        foreach (var account in await _app.GetAccountsAsync())
            await _app.RemoveAsync(account);
        AccountName = null;
    }

    /// <summary>
    /// An access token for Microsoft Graph, without prompting.
    /// Throws <see cref="MsalUiRequiredException"/> when the user must sign in again.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_app is null) throw new InvalidOperationException("Microsoft sign-in isn't configured.");
        await EnsureCacheAsync();
        var account = (await _app.GetAccountsAsync()).FirstOrDefault()
                      ?? throw new MsalUiRequiredException(MsalError.UserNullError, "Not signed in.");
        var result = await _app.AcquireTokenSilent(Scopes, account).ExecuteAsync(ct);
        return result.AccessToken;
    }

    // Tokens are cached in %LOCALAPPDATA%\WorldClock\msal.cache, encrypted for the current Windows user (DPAPI).
    private Task EnsureCacheAsync() => _cacheReady ??= RegisterCacheAsync();

    private async Task RegisterCacheAsync()
    {
        Directory.CreateDirectory(CacheDirectory);
        var properties = new StorageCreationPropertiesBuilder("msal.cache", CacheDirectory).Build();
        var helper = await MsalCacheHelper.CreateAsync(properties);
        helper.RegisterCache(_app!.UserTokenCache);
    }
}
