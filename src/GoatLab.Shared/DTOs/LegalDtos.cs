namespace GoatLab.Shared.DTOs;

// Lawyer-fillable values exposed to the public Terms / Privacy pages so they
// can substitute the bracketed placeholders. All optional — the client falls
// back to "[BRACKETED PLACEHOLDER]" text when a field is null/blank.
public record LegalSettingsDto(
    string? EntityName,
    string? EntityType,
    string? State,
    string? BusinessAddress,
    string? ContactEmail,
    string? GoverningLawState,
    string? GoverningLawCounty,
    string? GoverningLawCity,
    string? DisputeResolution,
    bool Approved
);
