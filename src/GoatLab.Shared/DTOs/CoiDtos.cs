namespace GoatLab.Shared.DTOs;

public record CoiResultDto(double Coi, IReadOnlyList<CommonAncestorDto> CommonAncestors);

public record CommonAncestorDto(int GoatId, string? Name, double Contribution, int SirePathLength, int DamPathLength);
