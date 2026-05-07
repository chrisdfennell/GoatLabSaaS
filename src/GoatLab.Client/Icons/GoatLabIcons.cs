namespace GoatLab.Client.Icons;

public static class GoatLabIcons
{
    // Stylized goat-head silhouette for the navbar / page-header brand mark.
    // Designed for a 24x24 MudIcon viewBox so it reads at favicon-ish sizes.
    // Single fill (uses currentColor / Style on the MudIcon) — keep it that way.
    // Const name kept as "Hoof" to avoid touching 17 call sites; the path itself
    // is now a goat head (horns + ears + chin), not hooves.
    public const string Hoof =
        "<path d=\"M 6 7 L 4 3 L 8 5 L 12 3 L 16 5 L 20 3 L 18 7 L 21 10 L 17 11 L 18 16 L 12 22 L 6 16 L 7 11 L 3 10 Z\"/>";
}
