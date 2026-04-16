using GoatLab.Server.Services.Email;

namespace GoatLab.Tests;

// Templates are simple string interpolation but a regression here silently
// breaks transactional emails. A handful of assertions on key tokens catches
// the obvious mistakes (missing link, missing name, empty subject).
public class EmailTemplatesTests
{
    [Fact]
    public void ConfirmEmail_contains_user_name_and_link()
    {
        var (subject, html, text) = EmailTemplates.ConfirmEmail("Chris", "https://goatlab.test/confirm-email?userId=1&token=abc");

        Assert.Contains("Confirm your", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Chris", html);
        Assert.Contains("https://goatlab.test/confirm-email?userId=1&token=abc", html);
        Assert.Contains("https://goatlab.test/confirm-email?userId=1&token=abc", text);
    }

    [Fact]
    public void ConfirmEmail_escapes_display_name()
    {
        var (_, html, _) = EmailTemplates.ConfirmEmail("<script>alert(1)</script>", "https://goatlab.test/x");
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void PasswordReset_contains_link()
    {
        var (subject, html, text) = EmailTemplates.PasswordReset("Chris", "https://goatlab.test/reset-password?t=xyz");

        Assert.Contains("Reset", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://goatlab.test/reset-password?t=xyz", html);
        Assert.Contains("https://goatlab.test/reset-password?t=xyz", text);
    }

    [Theory]
    [InlineData(1, "day")]
    [InlineData(3, "days")]
    public void TrialEnding_pluralizes_day_noun(int days, string expected)
    {
        var (subject, html, text) = EmailTemplates.TrialEnding("Chris", "Farm", days, "https://goatlab.test/billing");
        Assert.Contains($"{days} {expected}", subject);
        Assert.Contains($"{days} {expected}", html);
        Assert.Contains($"{days} day(s)", text); // text variant uses "day(s)" for simplicity
    }

    [Fact]
    public void TrialEnding_includes_plan_name_and_billing_url()
    {
        var (_, html, text) = EmailTemplates.TrialEnding("Chris", "Farm", 2, "https://goatlab.test/billing");
        Assert.Contains("Farm", html);
        Assert.Contains("https://goatlab.test/billing", html);
        Assert.Contains("https://goatlab.test/billing", text);
    }

    [Fact]
    public void TeamInvitation_contains_inviter_farm_role_and_link()
    {
        var (subject, html, text) = EmailTemplates.TeamInvitation(
            "Alice", "Sunrise Farm", "Worker", "https://goatlab.test/accept-invite?token=xyz");

        Assert.Contains("Alice", subject);
        Assert.Contains("Sunrise Farm", subject);
        Assert.Contains("Alice", html);
        Assert.Contains("Sunrise Farm", html);
        Assert.Contains("Worker", html);
        Assert.Contains("https://goatlab.test/accept-invite?token=xyz", html);
        Assert.Contains("https://goatlab.test/accept-invite?token=xyz", text);
    }

    [Fact]
    public void TeamInvitation_escapes_inviter_and_farm_name()
    {
        var (_, html, _) = EmailTemplates.TeamInvitation(
            "<evil>", "<iframe>", "Worker", "https://goatlab.test/x");
        Assert.DoesNotContain("<evil>", html);
        Assert.DoesNotContain("<iframe>", html);
    }
}
