# GoatLab — Privacy Policy (DRAFT)

> **DRAFT — NOT LEGAL ADVICE — NOT YET IN EFFECT**
>
> This is a starting-point draft for review by qualified legal counsel.
> Brackets `[LIKE THIS]` mark items the lawyer must fill in. Do not
> publish this document as-is.

**Last updated:** [DATE TO BE SET WHEN PUBLISHED]
**Effective date:** [DATE TO BE SET WHEN PUBLISHED]

---

## 1. Who we are and what this policy covers

This Privacy Policy describes how **[LEGAL ENTITY NAME]** (operating as
"**GoatLab**", "**we**", "**us**", "**our**") collects, uses, shares, and
protects personal information about you ("**you**", "**user**") when you
use our software service at https://goatlab.app, our APIs, our mobile and
PWA clients, and any related products (the "**Service**").

This policy applies to information we collect through the Service. It does
not apply to information collected by third-party websites or services
that may be linked from the Service.

For a description of what we promise about the Service generally, see our
[Terms of Service](https://goatlab.app/terms).

## 2. Information we collect

### 2.1. Information you provide directly

- **Account information:** name, email address, password (stored as a
  one-way hash, never plaintext), display name, optional profile photo.
- **Two-factor authentication:** TOTP secrets (encrypted at rest), passkey
  / WebAuthn credential public keys.
- **Farm information:** farm name, location (city/state/country
  granularity, not GPS unless you set it), units preference (imperial /
  metric).
- **Billing information:** name, billing address, last 4 digits of payment
  card. **We do not store full card numbers, CVV, or bank-account
  numbers** — those are handled directly by Stripe and never reach our
  servers.
- **Animal records:** ear tags, breed, gender, dates of birth, registration
  numbers (ADGA / AGS), pedigree (sire/dam links), medical records,
  vaccinations, weights, breeding records, kidding records, milk logs,
  show records, sale records, photos and documents you upload.
- **Inventory and finance:** feed inventory, medicine cabinet items,
  purchases, sales, transactions you record.
- **Public listings:** information you choose to make publicly viewable
  via the public Farm page feature, including any contact email you
  publish.
- **Communications:** messages you send to support; opt-in preferences for
  marketing or alert emails.

### 2.2. Information we collect automatically

- **Authentication and session data:** session cookies, IP address, user
  agent, timestamps of login attempts (used for security and rate
  limiting).
- **Usage telemetry:** which pages you visit within the application, which
  features you use, how often. We use this for product analytics and
  capacity planning. We do **not** use third-party analytics trackers
  (e.g., Google Analytics) at this time.
- **Error reports:** when the application encounters an unhandled
  exception, technical details (stack trace, request URL, user ID,
  timestamp) are sent to our error-monitoring sub-processor (Sentry) so
  we can fix bugs.
- **Push notification tokens:** if you opt in to push notifications, your
  browser provides an endpoint URL plus public-key material; we store
  these to deliver notifications and delete them when you unsubscribe or
  the browser invalidates them.

### 2.3. Information from third parties

- **Stripe:** subscription status, billing-period dates, last invoice
  status. We do not receive your full payment-card details.
- **Animal registry imports (ADGA / AGS):** if you upload a registry CSV
  for import, we extract animal records from the file you provide. We do
  not pull data directly from registry APIs at this time.

### 2.4. Cookies and similar technologies

We use a small number of cookies, all classified as **strictly necessary**
or **functional**:

| Cookie | Purpose | Duration |
|---|---|---|
| `.AspNetCore.Identity.Application` | Authenticated session | Session or "Remember me" duration |
| `.AspNetCore.Antiforgery.*` | CSRF protection | Session |
| `goatlab.cookieConsent` | Records that you saw the cookie banner | Persistent |
| `goatlab.pushPromptDismissed` | Records that you dismissed the push prompt | Persistent |

We do **not** use third-party advertising or cross-site tracking cookies.

In addition to cookies, the Service uses **IndexedDB** (a browser storage
mechanism) to maintain an offline write queue so that data you record
without an internet connection is preserved and re-synced when you come
back online.

## 3. How we use information

We use the information we collect to:

- Provide, maintain, secure, and improve the Service.
- Authenticate you and protect against unauthorized access.
- Process subscriptions, billing, refunds, and tax compliance.
- Send transactional emails (account confirmation, password reset, trial
  ending, billing receipts, alert digests, team invitations).
- Display alerts and reminders based on your data (e.g., medication
  overdue, kidding upcoming, feed low).
- Fulfill product features (PDF generation, CSV export, public listings,
  COI calculations).
- Diagnose and fix technical issues.
- Communicate with you about the Service, updates, and security.
- Comply with applicable laws and respond to lawful requests.

We **do not**:

- Sell your personal information to anyone.
- Share your animal records with breeders, registries, marketplaces, or
  any third party except as you direct (e.g., enabling your public Farm
  page or generating a sales contract PDF).
- Use your data to train machine-learning models.
- Send marketing email without your opt-in.

## 4. Legal basis for processing (GDPR)

If you are in the European Economic Area, the United Kingdom, or
Switzerland, our legal basis depends on the activity:

- **Performance of a contract** — to provide the Service you requested.
- **Legitimate interests** — to secure the Service, prevent abuse, fix
  bugs, and improve the product. We balance these interests against your
  rights and freedoms.
- **Consent** — for optional features such as push notifications,
  marketing email, and the public Farm page.
- **Legal obligation** — to comply with tax, accounting, and law
  enforcement requirements.

You may withdraw consent at any time without affecting the lawfulness of
prior processing.

## 5. Sharing and sub-processors

We share personal information only with the following categories of
recipients:

### 5.1. Sub-processors

| Sub-processor | Purpose | Location | Data shared |
|---|---|---|---|
| **Stripe, Inc.** | Payment processing | USA | Email, name, billing address, payment card (collected by Stripe directly) |
| **Sendinblue SAS d/b/a Brevo** | Transactional email | France / EU | Email address, name, email content |
| **Hostinger International Ltd.** | Hosting infrastructure | Lithuania, EU | All data stored on the servers (encrypted at rest) |
| **Functional Software, Inc. d/b/a Sentry** | Error monitoring | USA | User ID, request URL, stack traces, error context (no Customer Content) |
| **Google LLC** | Maps API (optional) | USA | Map view geo-coordinates and usage metrics; does not include animal records |

We have data-processing agreements (DPAs) in place with each sub-processor
where required by applicable law.

### 5.2. Legal disclosures

We may disclose information when we believe in good faith that disclosure
is necessary to (a) comply with a legal obligation, court order, or
government request; (b) enforce these Terms; (c) detect, prevent, or
address fraud, security, or technical issues; or (d) protect the rights,
property, or safety of GoatLab, our users, or the public.

We will give you notice of a government request for your data unless
prohibited by law.

### 5.3. Business transfers

If we are involved in a merger, acquisition, financing due diligence,
reorganization, bankruptcy, or sale of all or part of our assets, your
information may be transferred to the successor entity, subject to the
terms of this Privacy Policy.

### 5.4. With your direction

We share information when you direct us to — for example, by inviting a
team member to your Farm, enabling your public Farm page, or sending a
sales contract PDF to a buyer.

## 6. International data transfers

GoatLab is operated from the **[OPERATOR LOCATION — likely USA]**. Our
primary hosting infrastructure is in the **European Union (Lithuania)**.
Some sub-processors (Stripe, Sentry, Google) are headquartered in the
United States. By using the Service you consent to the transfer of your
data to these jurisdictions.

For transfers from the EEA, UK, or Switzerland to countries that have not
been deemed adequate, we rely on Standard Contractual Clauses where
required.

## 7. Data retention

We retain personal information only as long as needed to provide the
Service and to comply with our legal obligations:

- **Active accounts:** for the duration of your subscription.
- **Soft-deleted accounts and Farms:** **30 days** before permanent
  deletion. Cancellable on request during this window.
- **Billing records:** as required by tax law, typically **7 years**.
- **Server logs and request audit trails:** **14 days**.
- **Backups:** rotation policy of **30 days** for full database backups.

When we delete your data, we delete it from primary databases immediately
and from backups as backups age out of the rotation window.

## 8. Your rights

Depending on your jurisdiction you may have the following rights:

- **Access** — request a copy of the personal information we hold about
  you. Use the **Export my data** button at `/account/settings`.
- **Rectification** — correct inaccurate information. Most fields are
  editable directly in the Service; for others, contact us.
- **Erasure** — request that we delete your account and associated data.
  Use the **Delete my account** button at `/account/settings`. Subject to
  the 30-day grace period.
- **Restriction / objection** — ask us to limit or stop certain processing.
- **Portability** — receive your data in a machine-readable format. The
  export feature provides this.
- **Withdraw consent** — for any processing based on consent.
- **Lodge a complaint** with your local data-protection authority.

**California residents** have additional rights under the CCPA / CPRA,
including the right to know what categories of information we collect, to
delete, and to opt out of the "sale" or "sharing" of personal information
(we do not sell or share for cross-context behavioral advertising).

To exercise any right, email us at **[CONTACT EMAIL]**. We will respond
within **30 days**.

## 9. Children's privacy

The Service is not intended for individuals under **16 years of age**
(under 13 in the United States per COPPA). We do not knowingly collect
personal information from children. If you believe a child has provided
us information, contact us and we will delete it.

## 10. Security

We use commercially reasonable technical and organizational measures to
protect your information, including:

- **TLS 1.2+** encryption in transit for all client and webhook traffic.
- **Encryption at rest** for the database and offsite backups.
- **One-way password hashing** with PBKDF2 / Argon2 (no plaintext
  passwords stored).
- **Two-factor authentication** (TOTP and WebAuthn / passkeys) available
  for all accounts.
- **Rate limiting** on authentication endpoints.
- **Audit logging** of administrative actions.
- **Segregation** of customer data via tenant isolation enforced at the
  database query layer.

No system is perfectly secure. If we discover a breach affecting your
personal information, we will notify you and applicable regulators within
the timeframes required by law.

## 11. Public information

When you enable your public Farm page or list a goat for sale, the
information you choose to publish (farm name, location, animal photos,
breed, registration, asking price, contact email, pedigree to depth 2)
becomes accessible to anyone with the URL, including search engines and
caching services. **Do not publish information you would not want
publicly indexed.** You can disable the public page at any time from
`/farm-settings`, but cached copies may persist on third-party services
beyond our control.

## 12. Marketing communications

Transactional emails (account confirmation, password reset, billing,
trial reminders, alert digests) are part of the Service and cannot be
disabled while you have an active account, except for the alert digest
which you can opt out of at `/farm-settings`.

We do not currently send promotional or marketing email. If we add
marketing communications in the future, we will obtain your opt-in
consent first, and every such message will include an unsubscribe link.

## 13. Changes to this policy

We may update this Privacy Policy from time to time. The "Last updated"
date at the top reflects the most recent revision. For material changes
that affect how we process your information, we will provide reasonable
notice via email and/or in-app banner before the change takes effect.
Your continued use of the Service after the effective date constitutes
acceptance.

## 14. Contact

For privacy questions, requests, or complaints, email us at:

**[CONTACT EMAIL]**

Or by mail at:

**[BUSINESS ADDRESS]**

If you are in the EEA, UK, or Switzerland and believe your local
data-protection authority would be the appropriate venue for a complaint,
you may also contact them directly.
