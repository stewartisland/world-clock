using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WorldClock.Sync;

/// <summary>The saved settings file and its version tag.</summary>
public sealed record RemoteFile(string Json, string ETag);

/// <summary>Thrown when the saved file changed since it was last read (or was created by another PC).</summary>
public sealed class SyncConflictException() : Exception("The saved settings changed on another device.");

/// <summary>
/// Reads and writes clocks.json in the app's private OneDrive folder (Microsoft Graph "approot", which appears
/// as OneDrive\Apps\World Clock). With Files.ReadWrite.AppFolder the app can't see any other files.
/// </summary>
public sealed class OneDriveStore(MicrosoftAuth auth) : ISettingsStore
{
    private const string FileUrl = "https://graph.microsoft.com/v1.0/me/drive/special/approot:/clocks.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>The saved file, or null if nothing has been saved yet.</summary>
    public async Task<RemoteFile?> DownloadAsync(CancellationToken ct = default)
    {
        using var metadata = await SendAsync(HttpMethod.Get, FileUrl, null, null, ct);
        if (metadata.StatusCode == HttpStatusCode.NotFound) return null;
        metadata.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await metadata.Content.ReadAsStringAsync(ct));
        var eTag = doc.RootElement.GetProperty("eTag").GetString()!;
        var downloadUrl = doc.RootElement.GetProperty("@microsoft.graph.downloadUrl").GetString()!;

        // The download URL is pre-authenticated and short-lived, so it's fetched without a token.
        var json = await Http.GetStringAsync(downloadUrl, ct);
        return new RemoteFile(json, eTag);
    }

    /// <summary>
    /// Saves the file. With <paramref name="ifMatchETag"/>, only if it hasn't changed since; with null, only if it
    /// doesn't exist yet. Returns the new version tag. Throws <see cref="SyncConflictException"/> otherwise.
    /// </summary>
    public async Task<string> UploadAsync(string json, string? ifMatchETag, CancellationToken ct = default)
    {
        var url = FileUrl + ":/content" + (ifMatchETag is null ? "?@microsoft.graph.conflictBehavior=fail" : "");
        using var response = await SendAsync(HttpMethod.Put, url, json, ifMatchETag, ct);
        if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
            throw new SyncConflictException();
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("eTag").GetString()!;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, string? body, string? ifMatch, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await auth.GetTokenAsync(ct));
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return await Http.SendAsync(request, ct);
    }
}
