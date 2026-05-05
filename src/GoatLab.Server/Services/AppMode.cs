namespace GoatLab.Server.Services;

// Switches between the hosted SaaS deployment (goatlab.app — billing, public
// marketplace, plan tiers, trial reminders) and a self-hosted OSS deployment
// (single docker-compose, no Stripe, all features unlocked, no cross-tenant
// public surfaces). Read from `Saas:Enabled` config; defaults to true so the
// hosted build doesn't accidentally fall into OSS mode if the key is missing.
public interface IAppMode
{
    bool IsSaas { get; }
    bool IsOss => !IsSaas;
}

public sealed class AppMode : IAppMode
{
    public bool IsSaas { get; }

    public AppMode(IConfiguration config)
    {
        IsSaas = config.GetValue<bool?>("Saas:Enabled") ?? true;
    }
}
