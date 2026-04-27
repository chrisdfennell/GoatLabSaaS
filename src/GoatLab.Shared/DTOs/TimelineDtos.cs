namespace GoatLab.Shared.DTOs;

// One entry on a goat's chronological timeline. Aggregated server-side from
// 12+ sources (medical, weight, FAMACHA, body condition, milk, breeding,
// kidding, harvest, shows, appraisals, photos, documents, status changes).
//
// Severity drives color: "info" (default), "success" (births, wins),
// "warning" (FAMACHA 4-5, weight drop), "error" (deceased, illness).
//
// DeepLink is a relative client route; UI renders it as the entry's title link.
public record TimelineEntryDto(
    DateTime Date,
    string Kind,
    string Title,
    string? Detail,
    string? DeepLink,
    string Severity,
    string? Icon
);
