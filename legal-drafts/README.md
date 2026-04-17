# GoatLab legal drafts

This directory contains **draft** Terms of Service and Privacy Policy
documents for GoatLab. They are **not deployed**; the live `/terms` and
`/privacy` pages still show placeholder boilerplate with a "do not rely on
this" banner.

## Workflow

1. **Send the two `.md` files in this folder to your attorney** for red-mark.
2. They will modify, ask questions, and likely add jurisdiction-specific
   clauses (governing law, arbitration, consumer-protection carve-outs).
3. Once approved, transcribe the final text into:
   - `src/GoatLab.Client/Pages/Legal/Terms.razor`
   - `src/GoatLab.Client/Pages/Legal/Privacy.razor`
4. Remove the warning `MudAlert` at the top of each page.
5. Update the `Last updated:` date in both pages.
6. Commit + push + redeploy.
7. Have your attorney sign off in writing on the deployed final versions
   before accepting the first paying customer.

## What these drafts assume about GoatLab

These drafts are tailored to GoatLab's actual architecture and feature set:

- **Multi-tenant SaaS** — each "Farm" is a tenant; users can belong to
  multiple farms with Owner/Member/Viewer roles.
- **Subscription billing via Stripe** — Homestead (free), Farm, and Dairy
  paid tiers with 14-day trials on paid plans.
- **Sub-processors:**
  - **Stripe, Inc.** (USA) — payment processing
  - **Brevo / Sendinblue SAS** (France/EU) — transactional email
  - **Hostinger International Ltd.** (Lithuania/Cyprus) — VPS hosting
  - **Sentry / Functional Software, Inc.** (USA) — error monitoring
  - **Anthropic** is **NOT currently used** in production (deferred AI
    features); add later if you wire Claude vision.
- **User-generated content:** goat records (medical, breeding, milk, sales,
  finance, inventory), photos, documents, public farm pages.
- **Web Push notifications** via VAPID (subscriptions stored locally).
- **Offline queue** in browser IndexedDB (re-syncs on reconnect).
- **Account export + soft-delete** with 30-day grace period before hard delete.
- **Cookie consent banner** present.
- **2FA / Passkeys** available via TOTP + WebAuthn.

## Things the lawyer needs to decide

These appear as `[BRACKETED]` placeholders in the drafts:

- **Legal entity name** — sole proprietor, LLC, S-corp, etc.
- **Business mailing address** — for legal service of process.
- **Governing law jurisdiction** — likely your home state.
- **Dispute resolution** — court vs binding arbitration vs both with
  carve-outs (small claims, IP).
- **Class action waiver** — common in US SaaS; check enforceability per state.
- **Liability cap** — typically capped at fees paid in last 12 months.
- **Refund policy** — typically "no refunds" for SaaS, with exceptions for
  faulty service.
- **GDPR specifics** — if you have EU users, you need a Data Processing
  Addendum, lawful basis declarations, and possibly a EU representative.
- **CCPA specifics** — if you have California users, "Do Not Sell My Info"
  link, and consumer rights workflow.
- **Children's privacy** — COPPA prohibits collection from under-13s; we
  state "16+" as the minimum age but the lawyer should confirm.

## Re-publishing

Once the lawyer-approved final versions ship, **delete this folder** so
nobody mistakes the drafts for the live policies.
