namespace Prismedia.Application.Security;

/// <summary>
/// Port for copying one user's engagement state (watch progress, played, favorites,
/// ratings) onto another user. Used when first-run setup creates a brand-new admin on
/// an upgraded install: the migrated accounts each carry an identical copy of the
/// pre-multi-user household state, and the new admin inherits it so the household
/// owner's continue-watching survives the upgrade.
/// </summary>
public interface IUserEngagementCloner {
    /// <summary>
    /// Copies engagement state from any existing user onto <paramref name="targetUserId"/>.
    /// No-ops when there is no source user or the target already has state.
    /// </summary>
    Task CloneFromAnyUserAsync(Guid targetUserId, CancellationToken cancellationToken);
}
