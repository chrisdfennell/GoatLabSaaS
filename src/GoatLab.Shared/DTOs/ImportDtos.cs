namespace GoatLab.Shared.DTOs;

public record ImportRowError(int Row, string Message);

public record GoatImportResult(
    int TotalRows,
    int Imported,
    int Skipped,
    IReadOnlyList<ImportRowError> Errors
);
