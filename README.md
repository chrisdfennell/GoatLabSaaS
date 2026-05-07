# GoatLab

A multi-tenant goat-farm management platform for breeders, dairy operators,
and homesteaders. Same codebase runs in two modes:

- **Hosted SaaS** — [goatlab.app](https://goatlab.app). Stripe billing,
  plan-tier feature gating, cross-tenant marketplace, public farm pages,
  buyer accounts.
- **Self-host (OSS)** — `docker compose -f docker-compose.oss.yml up`.
  No Stripe, no plan tiers, every feature unlocked, marketplace + buyer
  flows hidden. First user to register becomes super-admin.

The toggle is a single config flag: `Saas:Enabled` (default `true` in the
hosted config, `false` in the OSS compose). See [Self-hosting](#self-hosting)
below.

## Features

A non-exhaustive tour:

- **Herd management** — goats, breeds, ear tags, photos, documents, pedigree
  trees, barns and pens, public for-sale listings.
- **Health** — medical records with auto-recurring due dates, vaccination
  protocols, FAMACHA + body-condition scoring, weight tracking, medication
  withdrawal holds for milk and meat.
- **Breeding & milk** — heat detection, kidding records and calendar,
  lactations + milk test days, daily milk trends.
- **Sales & finance** — sales pipeline, customers, deposits, transactions,
  purchases, P&L, cost-per-goat reports.
- **Operations** — calendar with recurring chores, checklists, shows + linear
  appraisals, farm map with pasture rotations, KML export.
- **Pedigree tools** — Wright's coefficient of inbreeding, mate
  recommendations scored on COI + production data.
- **Buyer waitlist + deposits** — Stripe Checkout for online reservations,
  buyer portal magic-link, fulfilment pipeline.
- **Reports & forecasting** — P&L, milk trends, kidding, mortality, parasite
  scoring, health spend, progeny rollups; trailing-average forecasts for
  kidding load, milk, cash flow.
- **Farm-to-farm transfers** — token-based handoff that moves a goat (and
  its medical/weight/milk/photo history) between two GoatLab tenants.
- **Smart alerts** — overdue meds, upcoming kiddings, low feed, weight drops;
  fans out via web push if enabled.
- **API + outbound webhooks** — tenant API keys, signed webhooks with
  retries.
- **PWA** — offline write queue, push notifications, install on mobile.

Plans (homestead/farm/dairy) are database rows, not enum values; admin can
edit pricing, feature toggles, and limits without a redeploy. See
[`/admin/plans`](https://goatlab.app/admin/plans).

## Tech stack

- **.NET 10** ASP.NET Core (server) + **Blazor WebAssembly** (client).
- **EF Core 10** + SQL Server 2022. Auto-migrate on startup.
- **MudBlazor** UI, Blazor-ApexCharts, QuestPDF, QRCoder.
- **Hangfire** for recurring jobs (alerts, trial reminders, hard-delete
  sweeps, offsite backups, webhook retries).
- **Stripe** subscriptions + Checkout. **Brevo** SMTP. **Sentry** error
  monitoring. **Caddy** reverse proxy with Let's Encrypt in production.
- **Fido2NetLib** for passkeys, **MailKit** for email, **AWSSDK.S3** for
  offsite backups (works with B2, Wasabi, Spaces, MinIO, etc).

## Repo layout

```
src/
  GoatLab.Server/   ASP.NET Core API + DbContext + migrations + Hangfire jobs
  GoatLab.Client/   Blazor WASM frontend (pages, services, layouts)
  GoatLab.Shared/   Shared models, DTOs, enums (referenced by both)
tests/
  GoatLab.Tests/    xUnit + SQLite in-memory test DB
docker-compose.yml         Local dev (Mailpit, mkcert HTTPS, host port maps)
docker-compose.prod.yml    Hosted SaaS (Caddy + LE certs, Stripe wired)
docker-compose.oss.yml     Self-host (no Stripe / Sentry / Brevo required)
.env.example               Local-dev environment template
.env.prod.example          Production environment template
```

## Local development

Prerequisites: Docker Desktop, .NET 10 SDK (only if you want to run tests
outside the container).

```bash
cp .env.example .env
# Set SA_PASSWORD to something strong (8+ chars, upper/lower/digit/symbol).

# Optional: generate a local HTTPS cert for the dev container.
bash tools/certs/gen-app-cert.sh
# Add to your hosts file: 127.0.0.1  goatlab.local

docker compose up --build
```

Services that come up:

- App: <http://localhost:8090> or <https://goatlab.local:8443>
- SQL Server: `localhost:1433` (sa / your SA_PASSWORD)
- Mailpit (captured outgoing email): <http://localhost:8025>

First run will auto-create the database, run migrations, and seed the three
default plans + super-admin (any email in `SuperAdmin:Emails`).

### Tests

```bash
dotnet test tests/GoatLab.Tests/GoatLab.Tests.csproj
```

The suite uses an in-memory SQLite DB; no SQL Server needed.

## Production deployment

The live site at `goatlab.app` runs from this repo on a Hostinger VPS with
`docker-compose.prod.yml` (Caddy → goatlab → sqlserver). To deploy:

```bash
ssh root@<vps>
cd /opt/goatlab
git pull
docker compose -f docker-compose.prod.yml up -d --build goatlab
docker compose -f docker-compose.prod.yml logs goatlab --tail 50
```

Required production env vars are documented in [`.env.prod.example`](.env.prod.example).
The Stripe webhook lives at `/api/billing/webhook` and must be registered in
the Stripe dashboard with a matching `STRIPE_WEBHOOK_SECRET`.

### Backups

Daily SQL Server `BACKUP DATABASE` runs at 04:00 UTC (Hangfire). For
disaster recovery, set `BACKUP_OFFSITE_*` env vars to ship the `.bak` to an
S3-compatible bucket (B2 / Wasabi / Spaces / S3) — last-success state shows
on `/admin/health` and you can fire a manual run from there.

## Self-hosting

If you want to run GoatLab on your own server for one farm or a small group
of homesteads, pull the prebuilt image from Docker Hub
([`fennch/goatlab`](https://hub.docker.com/r/fennch/goatlab)) — multi-arch
(amd64 + arm64), so it runs on a regular x86 box, a Raspberry Pi, or an
arm64 NAS:

```bash
git clone https://github.com/chrisdfennell/GoatLabSaaS
cd GoatLabSaaS
cp .env.example .env
# Set SA_PASSWORD to something strong.

docker compose -f docker-compose.oss.yml up -d
# Open http://localhost:8090 — the first signup is your super-admin.
```

`docker-compose.oss.yml` references `fennch/goatlab:latest` by default. Pin a
specific release by setting `GOATLAB_IMAGE_TAG=1.2.3` in `.env`. To build
from source instead of pulling, swap the `image:` line on the `goatlab`
service for `build: .`.

What you get versus the hosted SaaS:

- Every feature unlocked for every tenant — no plan tiers, no upgrade
  prompts, no `[RequiresFeature]` attributes blocking anything.
- No Stripe / Brevo / Sentry required. The stack runs without any of them
  configured.
- Marketplace, buyer accounts, public farm pages, deposit checkout, the
  cross-tenant search and feeds — all hidden / 404 in OSS mode (those
  features only make sense for a hosted multi-farm platform).
- Local SMTP optional; the in-app message inbox works without it.

If you eventually run out into the kind of deploy that needs the
marketplace, set `Saas__Enabled=true` and the SaaS surface comes back —
you'll just need to wire Stripe and SMTP.

## Status

Active development; the hosted build is taking real money at goatlab.app.
See `/changelog` on the live site for what shipped recently.

## License

MIT — see [LICENSE](LICENSE).
