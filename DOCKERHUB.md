# GoatLab

Self-hostable goat-farm management software — herd records, health, breeding,
milk, sales, pedigree, calendar, and alerts. Built for a single farm or a
small group of homesteads sharing an instance.

If you're looking for the hosted version, that's at https://goatlab.app. The
source code for this image lives at
https://github.com/chrisdfennell/GoatLabSaaS and is MIT-licensed.

## Get it running

You need Docker and about five minutes. GoatLab uses SQL Server 2022 as its
database, but you don't have to install anything separately — the included
compose file brings up both the app and the database together.

```bash
git clone https://github.com/chrisdfennell/GoatLabSaaS
cd GoatLabSaaS
cp .env.example .env
```

Open `.env` and pick a strong `SA_PASSWORD` — at least 8 characters with
upper-case, lower-case, a digit, and a symbol. SQL Server is picky about
this and refuses to start without it.

Then:

```bash
docker compose -f docker-compose.oss.yml up -d
```

Open http://localhost:8090. The first email you register with becomes the
super-admin for the whole instance, so make sure it's yours.

That's it — you're running.

## Picking a version

By default the compose file pulls `fennch/goatlab:latest`, which always
points at the most recent release. For production you probably want to pin
a specific version — set `GOATLAB_IMAGE_TAG=1.0.0` in your `.env` and that
tag will stick until you change it.

The image is multi-arch, so the same tag works on a regular x86 server, a
Raspberry Pi, or an arm64 NAS without you having to think about it.

## Putting it on the internet

If you only ever use GoatLab over `http://localhost`, skip this section.

Once you put a real domain in front of it — Caddy, Nginx, Traefik,
Cloudflare Tunnel, whatever — you need to tell GoatLab what domain to
expect. Without this, passkeys and a few other browser-side features will
refuse to work, because the browser sees one domain and the app expects
another.

Add this to your `.env`:

```
WEBAUTHN_RP_ID=goats.example.com
WEBAUTHN_ORIGIN=https://goats.example.com
```

Restart the container and you're set.

## Email

Out of the box, GoatLab works without email. You can ignore this section
until you want password resets, daily alert digests, or invitations to
actually arrive in someone's inbox.

When you're ready, fill in the `SMTP_*` variables in `.env`. Most providers
— Brevo, SendGrid, AWS SES, or your own mail server — will give you the
host, port, username, and password to drop in.

One trap to avoid: don't set `IDENTITY_REQUIRE_CONFIRMED_EMAIL=true` before
SMTP actually works. If you do, the first user gets locked out because the
confirmation email goes nowhere and login refuses unconfirmed accounts.
Wire SMTP first, register a real account, *then* turn confirmation on.

## Where your data lives

The compose file creates four named Docker volumes:

- **`goatlab-mssql`** — the database itself. The big one.
- **`goatlab-media`** — uploaded photos and documents.
- **`goatlab-backups`** — nightly database backups (`.bak` files).
- **`goatlab-dpkeys`** — ASP.NET keys for session cookies and passkeys.

Back up the first three. The fourth regenerates if it disappears, but
losing it logs everyone out and invalidates registered passkeys.

## Backups

GoatLab runs a SQL backup every night at 04:00 UTC and writes it to
`/app/backups` inside the container (the `goatlab-backups` volume). You can
also kick one off manually from the `/admin/health` page.

For offsite copies, set the `BACKUP_OFFSITE_*` variables in `.env`. Anything
S3-compatible works — Backblaze B2, Wasabi, DigitalOcean Spaces, MinIO, or
plain AWS S3. The last-success timestamp shows on `/admin/health` so you
can confirm it's actually running.

## Updating

```bash
docker compose -f docker-compose.oss.yml pull goatlab
docker compose -f docker-compose.oss.yml up -d goatlab
```

Database migrations run automatically the next time the container starts.
If one fails, the container refuses to come up rather than corrupting
anything — `docker compose logs goatlab` will tell you what went wrong.

## What this is vs. the hosted version

This image runs in **self-host mode** by default, which is almost certainly
what you want. Compared to goatlab.app, that means:

- Every feature is unlocked for every tenant. No plan tiers, no upgrade
  prompts, no paywalls.
- No Stripe, Brevo, or Sentry account required. The stack runs without any
  third-party SaaS configured.
- The cross-tenant marketplace, buyer accounts, public farm pages, and
  deposit checkout are hidden — those only make sense for a hosted
  multi-farm platform.

If you ever decide to run a paid service for multiple farms, you can flip
`Saas__Enabled=true` and the SaaS surface comes back. You'll just need to
wire up Stripe and SMTP at that point.

## Something's broken

- **Container exits immediately.** Run `docker compose logs goatlab`. Nine
  times out of ten it's the SA password not meeting complexity, or the app
  can't reach the database container.
- **First user can't log in.** You probably set
  `IDENTITY_REQUIRE_CONFIRMED_EMAIL=true` before SMTP was working. Set it
  back to `false`, log in, then turn it on once email is wired.
- **Passkeys won't register.** Your `WEBAUTHN_RP_ID` doesn't match the
  hostname in the browser's URL bar. They have to match exactly.
- **Migrations fail on startup.** Don't roll the image back without
  restoring a database backup from before you upgraded — migrations only
  run forward.

For anything else, open an issue at
https://github.com/chrisdfennell/GoatLabSaaS/issues. PRs welcome.
