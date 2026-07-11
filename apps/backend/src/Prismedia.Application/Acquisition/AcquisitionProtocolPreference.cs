using Prismedia.Application.Settings;
using Prismedia.Domain.Entities;

namespace Prismedia.Application.Acquisition;

/// <summary>
/// Resolves the effective transfer preference against enabled download-client capabilities and applies
/// it consistently after candidates cross persistence boundaries. A sole enabled protocol always wins;
/// a configured preference is meaningful only when both protocols are available.
/// </summary>
public static class AcquisitionProtocolPreference {
    /// <summary>Returns the effective preferred protocol, or null when no download protocol is enabled.</summary>
    public static async Task<DownloadProtocol?> ResolveAsync(
        IDownloadClientConfigStore downloadClients,
        SettingsService settings,
        CancellationToken cancellationToken) {
        var enabled = (await downloadClients.GetEnabledProtocolsAsync(cancellationToken)).Distinct().ToArray();
        if (enabled.Length == 0) {
            return null;
        }
        if (enabled.Length == 1) {
            return enabled[0];
        }

        var configured = (await settings.GetPreferredDownloadProtocolSettingsAsync(cancellationToken)).Protocol;
        return enabled.Contains(configured) ? configured : enabled[0];
    }

    /// <summary>Orders accepted candidates by effective protocol preference, then by their quality score.</summary>
    public static IOrderedEnumerable<T> Order<T>(
        IEnumerable<T> candidates,
        DownloadProtocol? preferredProtocol,
        Func<T, DownloadProtocol> protocol,
        Func<T, double> score) =>
        candidates
            .OrderByDescending(candidate => preferredProtocol is not null && protocol(candidate) == preferredProtocol)
            .ThenByDescending(score);
}
