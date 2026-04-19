namespace GoatLab.Shared.DTOs;

public record PublicGoatDto(
    int Id,
    string Name,
    string? EarTag,
    string? Breed,
    string Gender,
    DateTime? DateOfBirth,
    string? Bio,
    string? Registry,
    string? RegistrationNumber,
    string? BreederName,
    int? AskingPriceCents,
    string? SaleNotes,
    string FarmName,
    string FarmSlug,
    string? FarmLocation,
    string? FarmContactEmail,
    IReadOnlyList<PublicGoatPhotoDto> Photos,
    PublicPedigreeNodeDto? Pedigree,
    // Non-null when the farm has a deposit percent configured and the goat has
    // an asking price. Clients show a "Reserve with deposit" CTA that POSTs to
    // /api/public/farms/{slug}/goats/{id}/reserve to start Stripe Checkout.
    int? DepositCents
);

// Request body for the public reservation endpoint. Email + name are required
// so we can provision a Customer row on deposit success.
public record PublicReservationRequest(
    string BuyerEmail,
    string BuyerName,
    string? BuyerPhone,
    string? Notes
);

public record PublicGoatPhotoDto(string Url, string? Caption, bool IsPrimary);

public record PublicPedigreeNodeDto(
    int? Id,
    string? Name,
    string? RegistrationNumber,
    string? Breed,
    PublicPedigreeNodeDto? Sire,
    PublicPedigreeNodeDto? Dam
);

public record PublicGoatListItemDto(
    int Id,
    string Name,
    string? EarTag,
    string? Breed,
    string Gender,
    DateTime? DateOfBirth,
    int? AskingPriceCents,
    string? PrimaryPhotoUrl
);

// Breed directory — groups public listings across all tenants by normalized breed.
public record BreedSummaryDto(
    string BreedSlug,
    string DisplayName,
    int FarmCount,
    int ListingCount);

public record BreedFarmDto(
    string FarmSlug,
    string FarmName,
    string? Location,
    int ListingCount,
    int? StartingPriceCents,
    string? HeroPhotoUrl);
