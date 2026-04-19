using GoatLab.Server.Controllers;

namespace GoatLab.Tests;

public class BreedSlugTests
{
    [Theory]
    [InlineData("Nigerian Dwarf", "nigerian-dwarf")]
    [InlineData("nigerian dwarf", "nigerian-dwarf")]
    [InlineData("  Nigerian Dwarf  ", "nigerian-dwarf")]
    [InlineData("Nigerian-Dwarf", "nigerian-dwarf")]
    [InlineData("Nigerian   Dwarf", "nigerian-dwarf")]
    [InlineData("LaMancha", "lamancha")]
    [InlineData("Nubian / Alpine", "nubian-alpine")]
    [InlineData("50% Boer", "50-boer")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Slugs_normalize_consistently(string? input, string expected)
    {
        Assert.Equal(expected, PublicController.BreedSlug(input));
    }
}
