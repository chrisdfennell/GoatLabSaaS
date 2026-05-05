using GoatLab.Server.Controllers;
using GoatLab.Server.Services.Webhooks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatLab.Tests;

// Pinning the gestation calculator's "days remaining" math after the fix
// from .Days (truncates fractional hours) to ceiling on TotalDays.
// A doe due in 18 hours used to show "0 days remaining" — now shows 1.
public class GestationCalculatorTests
{
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private static BreedingController NewController(TestDb db) =>
        new(db.Context,
            new WebhookDispatcher(
                db.Context, db.Tenant,
                new StubHttpClientFactory(), NullLogger<WebhookDispatcher>.Instance));

    private static int ExtractDaysRemaining(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        var prop = ok.Value!.GetType().GetProperty("daysRemaining");
        Assert.NotNull(prop);
        return (int)prop!.GetValue(ok.Value)!;
    }

    [Fact]
    public void Eighteen_hours_remaining_rounds_up_to_one_day()
    {
        using var db = new TestDb();
        var ctrl = NewController(db);

        // Due 18 hours from now → previously truncated to 0 days, masked the
        // "due tomorrow" banner. Should report 1.
        var breedingDate = DateTime.UtcNow.AddDays(-150).AddHours(18);
        var result = (ctrl.CalculateGestation(breedingDate, 150) as ActionResult<object>)!.Result!;

        Assert.Equal(1, ExtractDaysRemaining(result));
    }

    [Fact]
    public void Already_overdue_clamps_to_zero()
    {
        using var db = new TestDb();
        var ctrl = NewController(db);

        // Bred 200 days ago, gestation 150 days → due was 50 days ago.
        var breedingDate = DateTime.UtcNow.AddDays(-200);
        var result = (ctrl.CalculateGestation(breedingDate, 150) as ActionResult<object>)!.Result!;

        Assert.Equal(0, ExtractDaysRemaining(result));
    }

    [Fact]
    public void Whole_number_of_days_remaining_is_preserved()
    {
        using var db = new TestDb();
        var ctrl = NewController(db);

        // Bred 100 days ago, gestation 150 → ~50 days remain. Allow ±1 for
        // the second elapsed between AddDays and DateTime.UtcNow inside the
        // controller; the contract is "no truncation," not exact equality.
        var breedingDate = DateTime.UtcNow.AddDays(-100);
        var result = (ctrl.CalculateGestation(breedingDate, 150) as ActionResult<object>)!.Result!;
        var days = ExtractDaysRemaining(result);

        Assert.InRange(days, 49, 51);
    }
}
