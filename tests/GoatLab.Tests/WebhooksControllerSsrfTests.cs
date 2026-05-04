using GoatLab.Server.Controllers;

namespace GoatLab.Tests;

// Pure unit tests for the SSRF host-block list. The validator runs at webhook
// registration time and rejects any host that resolves to a loopback /
// private network / link-local literal. We can't catch DNS rebinding from
// here — that's a runtime concern in the dispatcher — but this stops a
// tenant from accidentally (or intentionally) pointing a webhook at our
// own internal services or cloud metadata endpoints.
public class WebhooksControllerSsrfTests
{
    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("ip6-localhost")]
    [InlineData("ip6-loopback")]
    [InlineData("foo.localhost")]
    [InlineData("printer.local")]      // mDNS suffix
    [InlineData("api.internal")]
    [InlineData("127.0.0.1")]
    [InlineData("127.42.7.1")]
    [InlineData("10.0.0.5")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.1")]
    [InlineData("192.168.255.255")]
    [InlineData("169.254.169.254")]    // AWS / Azure metadata IP
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fe80::1")]            // IPv6 link-local
    [InlineData("fc00::1")]            // IPv6 ULA
    public void IsInternalHost_blocks_loopback_and_private_ranges(string host)
    {
        Assert.True(WebhooksController.IsInternalHost(host),
            $"Expected '{host}' to be flagged as internal but it wasn't.");
    }

    [Theory]
    [InlineData("example.com")]
    [InlineData("api.stripe.com")]
    [InlineData("hooks.slack.com")]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]         // just outside the 172.16/12 block
    [InlineData("169.255.0.1")]        // just outside the 169.254/16 block
    [InlineData("11.0.0.1")]           // just outside the 10/8 block
    [InlineData("192.169.0.1")]        // just outside the 192.168/16 block
    [InlineData("2606:4700:4700::1111")] // public IPv6 (Cloudflare)
    public void IsInternalHost_allows_public_hosts(string host)
    {
        Assert.False(WebhooksController.IsInternalHost(host),
            $"Expected '{host}' to be allowed but it was flagged as internal.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsInternalHost_treats_empty_as_internal(string host)
    {
        // Defensive: a webhook with no host shouldn't be allowed through.
        Assert.True(WebhooksController.IsInternalHost(host));
    }
}
