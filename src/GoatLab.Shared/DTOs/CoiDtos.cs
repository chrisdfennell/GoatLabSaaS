namespace GoatLab.Shared.DTOs;

public record CoiResultDto(double Coi, IReadOnlyList<CommonAncestorDto> CommonAncestors);

public record CommonAncestorDto(int GoatId, string? Name, double Contribution, int SirePathLength, int DamPathLength);

public record MateRecommendationDto(
    int BuckId,
    string BuckName,
    string? EarTag,
    string? Breed,
    DateTime? DateOfBirth,
    double ProjectedCoi,
    int KiddingsSired,
    int OffspringCount,
    double? AvgLitterSize,
    double? LiveBirthRate,
    double? DaughterMilkDailyAvgLbs,
    double CompositeScore,
    string Rationale);
