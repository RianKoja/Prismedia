using System.Text.Json;

namespace Prismedia.Application.Acquisition;

/// <summary>
/// Payload for acquisition background jobs (search, monitor, import): the acquisition id they act on.
/// <see cref="AllowFormatChange"/> rides only on a manual retry-import — the user's explicit consent for
/// an upgrade that swaps the owned file's format; absent (false) on every automatic enqueue.
/// <see cref="ManualRetry"/> marks any user-initiated retry-import ("import anyway"): it bypasses the
/// wrong-content hold (the user reviewed the payload) but never the dangerous-file hold.
/// </summary>
public sealed record AcquisitionJobPayload(Guid AcquisitionId, bool AllowFormatChange = false, bool ManualRetry = false) {
    public static string Serialize(Guid acquisitionId, bool allowFormatChange = false, bool manualRetry = false) =>
        JsonSerializer.Serialize(new AcquisitionJobPayload(acquisitionId, allowFormatChange, manualRetry));

    public static AcquisitionJobPayload Parse(string payloadJson) =>
        JsonSerializer.Deserialize<AcquisitionJobPayload>(payloadJson)
            ?? throw new InvalidOperationException("Acquisition job payload is missing or invalid.");
}
