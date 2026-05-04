using GoatLab.Server.Services.Email;

namespace GoatLab.Tests;

// SanitizeHeader is the defensive line between buyer-supplied strings and the
// email Subject header. MimeKit also rejects header injection on its own, but
// the helper keeps multi-line buyer names from rendering as wrapped subjects
// in inboxes that don't strictly enforce CRLF rules.
public class EmailTemplateSanitizationTests
{
    [Theory]
    [InlineData("Plain Name", "Plain Name")]
    [InlineData(" trim me \t", "trim me")]
    [InlineData("Foo\r\nBcc: attacker@evil.com", "FooBcc: attacker@evil.com")]
    [InlineData("Foo\nBar", "FooBar")]
    [InlineData("Foo\rBar", "FooBar")]
    [InlineData("Foo\0Bar", "FooBar")]      // NUL
    [InlineData("FooBar", "FooBar")]  // BEL
    [InlineData("FooBar", "FooBar")]  // ESC
    public void SanitizeHeader_strips_control_chars_and_trims(string input, string expected)
    {
        Assert.Equal(expected, EmailTemplates.SanitizeHeader(input));
    }

    [Fact]
    public void SanitizeHeader_returns_empty_for_null_or_empty_input()
    {
        Assert.Equal(string.Empty, EmailTemplates.SanitizeHeader(null));
        Assert.Equal(string.Empty, EmailTemplates.SanitizeHeader(""));
    }

    [Fact]
    public void SanitizeHeader_preserves_unicode_letters()
    {
        // Emoji + non-ASCII letters belong in real names; only control chars get cut.
        Assert.Equal("Élise 🐐", EmailTemplates.SanitizeHeader("Élise 🐐"));
    }

    [Fact]
    public void BuyerInquiryNew_subject_has_no_CRLF_when_buyer_name_contains_newlines()
    {
        var (subject, _, _) = EmailTemplates.BuyerInquiryNew(
            buyerName: "Mallory\r\nBcc: attacker@evil.com",
            buyerEmail: "m@example.com",
            buyerPhone: null,
            goatName: "Daisy",
            farmName: "Cedar Farm",
            messageBody: "hi",
            inboxUrl: "https://goatlab.app/inquiries");

        Assert.DoesNotContain("\n", subject);
        Assert.DoesNotContain("\r", subject);
        // The sanitized name should still be present, just without the CRLF.
        Assert.Contains("MalloryBcc", subject);
    }
}
