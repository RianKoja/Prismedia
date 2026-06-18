using System.Security.Cryptography;
using System.Text;
using Prismedia.Api.Security;

namespace Prismedia.Api.Endpoints;

internal static class JellyfinAudioPlaybackTracking {
    internal static string ClientKey(HttpContext httpContext) {
        var auth = httpContext.GetPrismediaAuth();
        var client = httpContext.Request.GetJellyfinClientIdentity();
        var deviceId = Normalized(client.DeviceId) ??
            Normalized(auth?.JellyfinSession?.Session.DeviceId);

        if (auth?.Kind == PrismediaAuthKind.JellyfinSession &&
            auth.JellyfinSession is { } session) {
            return deviceId is not null
                ? $"jellyfin:{session.Profile.Id:N}:device:{Hash(deviceId)}"
                : $"jellyfin:{session.Profile.Id:N}:session:{session.Session.Id:N}";
        }

        if (auth is not null) {
            return deviceId is not null
                ? $"{auth.Kind}:token:{Hash(auth.Token)}:device:{Hash(deviceId)}"
                : $"{auth.Kind}:token:{Hash(auth.Token)}";
        }

        var remote = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown";
        return $"anonymous:{Hash(remote)}:{Hash(userAgent)}";
    }

    internal static bool IsRangeRequest(HttpContext httpContext) =>
        httpContext.Request.Headers.Range.Count > 0;

    private static string? Normalized(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
