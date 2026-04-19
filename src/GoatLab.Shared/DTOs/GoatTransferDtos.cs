namespace GoatLab.Shared.DTOs;

// Seller-side list + detail.
public record GoatTransferSummaryDto(
    int Id,
    int GoatId,
    string GoatName,
    string BuyerEmail,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    DateTime? DeclinedAt,
    DateTime? CancelledAt,
    string TokenPrefix);

// Seller creates a transfer.
public record InitiateTransferRequest(int GoatId, string BuyerEmail, string? Message, int? ExpiryDays);

// Response — plaintext token shown ONCE so the seller can copy/paste the link
// if email delivery fails.
public record InitiateTransferResponse(
    int TransferId,
    string AcceptUrl,
    string PlaintextToken);

// Buyer preview (anon GET via token).
public record GoatTransferPreviewDto(
    int TransferId,
    string Status,
    DateTime ExpiresAt,
    string SellerFarmName,
    string GoatName,
    string? Breed,
    string Gender,
    DateTime? DateOfBirth,
    string? EarTag,
    string? RegistrationNumber,
    string? SireName,
    string? DamName,
    int MedicalRecordCount,
    int WeightRecordCount,
    int MilkLogCount,
    int PhotoCount,
    string? Message,
    string? PrimaryPhotoUrl);

// Buyer accepts — must pick which of their tenants receives the goat.
public record AcceptTransferRequest(int ToTenantId);

public record DeclineTransferRequest(string? Reason);

public record AcceptTransferResponse(int ToTenantId, int GoatId);
