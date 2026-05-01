# Reddit launch post — GoatLab

> Drop this into r/goats, r/homestead, r/smallfarms, r/dairy, or r/permaculture.
> Tweak the opening hook for the subreddit you're posting to (notes at the bottom).
> Reddit accepts standard markdown — bold, italics, lists, links all render fine.

---

## Title options

Pick one, A/B as you go:

- **I got tired of tracking my goats in spreadsheets, so I built a free tool that does the records AND lists them for sale**
- **Built a free goat-farm app — herd records, kidding, milk, vet history, and a public marketplace where buyers can find your goats**
- **GoatLab — open-source goat herd management with a built-in buyer marketplace (free)**

---

## Body

I run a small goat operation and got fed up bouncing between spreadsheets, a Notes app full of FAMACHA scores, and a Facebook group for sales. So I built **GoatLab** — one place to track every animal *and* sell them when the time comes.

It's free. Self-host it if you want, or use the hosted version at **https://goatlab.app**.

**What it actually does**

- **Herd records** — name, ear tag, registry #, tattoos, scrapie tag, microchip, breeder, photos, documents (vet bills, registration papers, etc.)
- **Health** — vaccinations, dewormings, FAMACHA scores, body condition, weights with auto-flagged drops, milk-withdrawal tracking
- **Breeding** — heat detection, kidding records, due-date forecasting, mate recommendations with COI calculator
- **Milk production** — daily logs, lactation curves, DHI test-day support
- **Pedigree** — three-gen ancestry, printable branded certificates, COI across the whole network
- **Calendar + chores** — recurring task checklists, iCal subscription feed (sync to Google Calendar / Apple Calendar)
- **Finances** — feed inventory, expenses, per-goat P&L, sales records
- **Public farm page** — anyone can browse your herd at goatlab.app/pub/{your-slug}, with optional **Stripe deposit reservations** so buyers can put money down before pickup
- **Marketplace** — buyers shop across every public farm by breed, sex, price, state. Saved-search alerts. New listing notifications.
- **Cross-farm pedigree** — when a goat moves between farms, the full record (medical, weight, photos, lineage) transfers with it. Buyers can verify ancestry across every farm on the network, not just yours.
- **Vet share-links** — give your vet read-only access to one goat's records without a login
- **Buyer messaging** — buyers can DM you about a listing without an account; replies arrive in your inbox
- **Offline-first PWA** — install on your phone, works in the barn with no signal, syncs when you're back in range

**Why it might be different from what you're using now**

Most goat trackers stop at records. Most marketplace sites don't track records. GoatLab does both, and connects them — your records *are* your sales listing, and pedigree walks across farms instead of being trapped in your file.

It's also free. There's a paid tier ($19.99/mo Farm, $49.99/mo Dairy) for advanced reporting and bulk features, but the homestead tier — which is plenty for most farms — is genuinely $0 with no card.

**What I'd love feedback on**

- What's missing? I'm a one-person dev shop and I'm still finding gaps
- Anything confusing in the signup or the public farm page setup?
- Breeders: would you actually use the deposit reservation flow, or is that solving a problem you don't have?
- Buyers: is the marketplace search useful, or do you just go to Facebook?

Honest critique welcome. If you find a bug or hate a UX choice, tell me — easier to fix while I'm small.

**Self-host**

The codebase is open source: https://github.com/chrisdfennell/GoatLab. Docker compose, .NET 10, SQL Server. Deploy guide is in the README. Run it on a $5 VPS, your own data, no SaaS dependency.

Try it: **https://goatlab.app**

Happy to answer anything in the comments.

---

## Subreddit-specific tweaks

**r/goats** — open with the personal hook ("I run a small herd and got tired of spreadsheets…"). Keep all the herd-management bullets. Cut the self-host paragraph (audience doesn't care).

**r/homestead** — emphasize "works offline in the barn", "free", "your data stays yours". Mention sheep/cattle would be future additions if anyone asks. Cut the "marketplace" framing — homesteaders are more skeptical of for-profit angles.

**r/smallfarms** — lean into the per-goat P&L, feed cost tracking, sales records. These folks care about money in/out per animal.

**r/selfhosted** — lead with the open-source angle and the docker-compose. Skip the goat-specific feature list, link to the GitHub README. Mention .NET 10 stack, SQL Server, Caddy reverse proxy.

**r/dairy** — emphasize milk production tracking, lactation curves, DHI, milk-withdrawal alerts. The marketplace is less interesting for dairy buyers.

---

## Posting hygiene

- Reddit's spam filter is brutal on first-time posters. Comment in the sub a few times before posting a launch.
- Don't post the same body across multiple subs in the same hour — automod flags cross-posts.
- Reply to every top-level comment in the first 2 hours. Engagement = visibility.
- If a mod removes it, ask politely what rule was broken — most are open to founder posts that follow the format.
- The phrase "I built" works better than "we built" on Reddit — solo developers get more goodwill than startups.
