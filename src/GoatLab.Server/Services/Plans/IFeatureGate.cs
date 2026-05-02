using GoatLab.Shared.Models;

namespace GoatLab.Server.Services.Plans;

public interface IFeatureGate
{
    /// <summary>Loads the current tenant's plan (with features). Null if no tenant context.</summary>
    Task<Plan?> GetCurrentPlanAsync(CancellationToken cancellationToken = default);

    /// <summary>True when the current tenant's plan has the feature enabled.</summary>
    Task<bool> IsEnabledAsync(AppFeature feature, CancellationToken cancellationToken = default);

    /// <summary>True when the tenant hasn't hit its MaxGoats cap (null = unlimited).</summary>
    Task<bool> CanAddGoatAsync(CancellationToken cancellationToken = default);

    /// <summary>True when the tenant hasn't hit its MaxUsers cap (null = unlimited).</summary>
    Task<bool> CanAddUserAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the tenant hasn't hit its MaxPublicListings cap (null =
    /// unlimited). Counts goats with IsListedForSale=true and IsExternal=false.
    /// </summary>
    Task<bool> CanAddPublicListingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the goat hasn't hit its plan's MaxPhotosPerGoat cap (null =
    /// unlimited). Used by the photo-upload endpoint to 402 over the cap.
    /// </summary>
    Task<bool> CanAddPhotoAsync(int goatId, CancellationToken cancellationToken = default);
}
