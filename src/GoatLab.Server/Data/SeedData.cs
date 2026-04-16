using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Data;

public static class SeedData
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        // Global reference data — shared across all tenants.
        SeedCareArticles(modelBuilder);
        SeedMedications(modelBuilder);
        // Checklist seeding disabled here because checklists are tenant-scoped.
        // Starter checklists are created per-tenant by TenantSeeder on signup.
    }

    private static void SeedCareArticles(ModelBuilder mb)
    {
        mb.Entity<CareArticle>().HasData(
            // Getting Started (1-5)
            new CareArticle { Id = 1, Title = "Choosing Your First Goats", Category = CareArticleCategory.GettingStarted, SortOrder = 1, IsBuiltIn = true,
                Summary = "How to pick the right breed and number of goats for your farm.",
                Content = @"Starting a goat herd is an exciting venture. Here are the key considerations:

**Breed Selection**
- **Dairy breeds**: Nubian, Alpine, Saanen, LaMancha — great milk producers
- **Meat breeds**: Boer, Kiko, Spanish — fast growth and muscling
- **Fiber breeds**: Angora, Cashmere — for mohair and cashmere fiber
- **Dual-purpose**: Nigerian Dwarf, Pygmy — milk and companionship

**How Many to Start With**
Goats are herd animals and should never be kept alone. Start with at least 2-3 goats. A small starter herd of 3-5 does and 1 buck is ideal for most beginners.

**Where to Buy**
- Reputable breeders with health records
- Local livestock auctions (inspect carefully)
- Breed associations and registries
- Other small farms in your area

**What to Look For**
- Bright, clear eyes
- Clean nose (no discharge)
- Good body condition (not too thin or fat)
- Sound feet and legs
- Up-to-date on vaccinations and deworming" },

            new CareArticle { Id = 2, Title = "Fencing & Shelter Basics", Category = CareArticleCategory.GettingStarted, SortOrder = 2, IsBuiltIn = true,
                Summary = "Essential fencing types and shelter requirements for goats.",
                Content = @"Goats are notorious escape artists. Good fencing is your first investment.

**Fencing Options**
- **Woven wire (4x4)**: Most popular, 4-foot minimum height
- **Electric**: Effective training fence, 5+ strands
- **Cattle panels**: Sturdy and long-lasting, great for small areas
- **Board fencing**: Attractive but goats may chew wood

**Shelter Requirements**
- Minimum 15-20 sq ft per goat
- Three-sided shelter is adequate in mild climates
- Must be dry and draft-free
- Good ventilation (ammonia buildup is dangerous)
- Bedding: straw, wood shavings, or hay

**Key Tips**
- Goats hate rain more than cold — keep them dry
- Provide shade in summer
- Separate bucks from does except during breeding
- Kidding stalls should be 5x5 ft minimum" },

            new CareArticle { Id = 3, Title = "Setting Up Your Farm for Goats", Category = CareArticleCategory.GettingStarted, SortOrder = 3, IsBuiltIn = true,
                Summary = "Infrastructure planning, pasture layout, and water systems.",
                Content = @"A well-planned farm layout saves time and prevents problems.

**Pasture Planning**
- Allow 250-500 sq ft per goat for exercise areas
- Rotational grazing: divide pastures into 3-4 paddocks
- Rest each paddock 4-6 weeks between grazing
- This breaks parasite cycles and improves forage quality

**Water Systems**
- Each goat drinks 2-4 gallons per day (more in summer/lactation)
- Automatic waterers reduce daily chores
- Keep water clean — goats are picky drinkers
- Heated buckets or de-icers for winter

**Feed Storage**
- Secure hay storage (dry, ventilated)
- Rodent-proof grain bins
- Mineral feeders accessible to goats but not weather
- Separate feeding areas to reduce bullying

**Essential Equipment**
- Hay feeders (keyhole style prevents waste)
- Grain feeders or troughs
- Mineral feeders (loose minerals preferred over blocks)
- Hoof trimming stand or milk stand
- Basic first aid kit" },

            new CareArticle { Id = 4, Title = "Understanding Goat Behavior", Category = CareArticleCategory.GettingStarted, SortOrder = 4, IsBuiltIn = true,
                Summary = "Herd dynamics, body language, and social structure.",
                Content = @"Understanding goat behavior helps you manage your herd effectively.

**Herd Hierarchy**
Goats establish a pecking order. The dominant doe (""queen"") eats first and gets the best sleeping spot. New additions may be bullied initially — introduce them gradually.

**Body Language**
- **Tail wagging**: Content, often during feeding
- **Stamping feet**: Alert/warning to others
- **Head butting**: Establishing dominance (normal)
- **Grinding teeth**: Pain or discomfort (investigate!)
- **Lip curling (flehmen)**: Buck detecting does in heat
- **Pawing ground**: Boredom or frustration

**Vocalizations**
- Short bleats: General communication
- Long, loud calls: Distress, hunger, or separation anxiety
- Soft murmurs: Doe to kid bonding
- Sneezing/snorting: Clearing dust or mild irritation

**Common Behaviors**
- Goats are browsers, not grazers — they prefer brush and weeds over grass
- They climb everything — secure your structures
- They're curious and will investigate (and taste) anything new" },

            new CareArticle { Id = 5, Title = "Legal Requirements & Zoning", Category = CareArticleCategory.GettingStarted, SortOrder = 5, IsBuiltIn = true,
                Summary = "Permits, zoning laws, and registration requirements for goat keeping.",
                Content = @"Before bringing goats home, check your local regulations.

**Zoning**
- Verify your property is zoned for livestock
- Some areas allow goats under 'small animal' or 'hobby farm' provisions
- Urban/suburban areas may have goat-specific ordinances
- HOAs may have additional restrictions

**Permits & Registration**
- Many states require a premises ID for livestock
- Scrapie tags or tattoos may be required for interstate transport
- Check if you need a livestock permit or farm plan
- Dairy sales have additional regulations (raw milk laws vary by state)

**Good Neighbor Practices**
- Keep pens clean to minimize odor
- Bucks smell strong during rut — plan pen placement accordingly
- Noise: goats can be vocal, especially at feeding time
- Maintain setback distances from property lines" },

            // Health & Care (6-11)
            new CareArticle { Id = 6, Title = "Common Goat Diseases & Prevention", Category = CareArticleCategory.HealthAndCare, SortOrder = 1, IsBuiltIn = true,
                Summary = "Overview of the most common health issues and how to prevent them.",
                Content = @"Prevention is always easier than treatment. Here are the most common issues:

**Parasites (Worms)**
The #1 health challenge for goats. Barber pole worm (Haemonchus contortus) causes anemia and can be fatal.
- Use FAMACHA scoring to monitor anemia
- Rotate pastures to break parasite cycles
- Deworm selectively, not on a fixed schedule
- Fecal egg counts help guide treatment

**Coccidiosis**
Common in kids, causes diarrhea and weight loss.
- Keep bedding dry and clean
- Preventive coccidiostats in feed for kids
- Treat with sulfa drugs or amprolium

**Enterotoxemia (Overeating Disease)**
Caused by Clostridium bacteria when goats overeat grain.
- Vaccinate with CDT vaccine annually
- Don't change feed suddenly
- Limit grain access

**Pneumonia**
Caused by stress, poor ventilation, or weather changes.
- Good ventilation in barns (not drafts)
- Reduce stress during transport/weather changes
- Vaccinate if endemic in your area

**Foot Rot / Foot Scald**
- Trim hooves every 6-8 weeks
- Keep housing dry
- Treat with zinc sulfate foot baths" },

            new CareArticle { Id = 7, Title = "Vaccination Schedule", Category = CareArticleCategory.HealthAndCare, SortOrder = 2, IsBuiltIn = true,
                Summary = "Core and optional vaccinations for goats by age.",
                Content = @"**Core Vaccines**

**CDT (Clostridium Perfringens Types C&D + Tetanus)**
This is the single most important vaccine for goats.
- Kids: First dose at 4-6 weeks, booster 3-4 weeks later
- Adults: Annual booster
- Pregnant does: Booster 4-6 weeks before kidding (passes immunity to kids through colostrum)

**Optional Vaccines (based on your area)**
- CLA (Caseous Lymphadenitis): If prevalent in your herd/area
- Rabies: Required in some areas, especially if wildlife contact possible
- Chlamydia: If abortion storms are a concern
- Pneumonia vaccines: In herds with chronic respiratory issues

**Vaccination Tips**
- Store vaccines properly (refrigerate, don't freeze)
- Use clean needles (18-20 gauge, 3/4 to 1 inch)
- SubQ (under the skin) injections in the neck or behind the shoulder
- Record everything — date, vaccine, lot number, goat ID
- Watch for rare allergic reactions for 30 minutes after injection" },

            new CareArticle { Id = 8, Title = "FAMACHA Scoring Guide", Category = CareArticleCategory.HealthAndCare, SortOrder = 3, IsBuiltIn = true,
                Summary = "How to check eyelid color to assess anemia from barber pole worm.",
                Content = @"FAMACHA is a simple field test to detect anemia caused by the barber pole worm.

**How to Score**
1. Restrain the goat gently
2. Pull down the lower eyelid
3. Compare the inner eyelid color to the FAMACHA card
4. Score 1-5:

**Score 1 — Red**: Optimal. Healthy, not anemic.
**Score 2 — Red-Pink**: Acceptable. Monitor.
**Score 3 — Pink**: Borderline. Consider deworming.
**Score 4 — Pink-White**: Anemic. Deworm immediately.
**Score 5 — White**: Severely anemic. Deworm and consider supportive care (iron, B12). May need veterinary attention.

**When to Check**
- Every 2-4 weeks during warm/wet months (peak parasite season)
- Monthly during cool/dry months
- Any time a goat looks lethargic or has a rough coat
- Before and after deworming to assess effectiveness

**Best Practices**
- Check in natural light for accurate color assessment
- Score consistently — same person if possible
- Record scores to track trends per goat
- Combine with fecal egg counts for full picture
- Only deworm goats scoring 3 or higher (selective deworming)" },

            new CareArticle { Id = 9, Title = "Body Condition Scoring", Category = CareArticleCategory.HealthAndCare, SortOrder = 4, IsBuiltIn = true,
                Summary = "Assess your goat's body fat and health by feel.",
                Content = @"Body Condition Scoring (BCS) rates a goat's fat reserves on a 1-5 scale by feel.

**How to Score**
Feel the goat over the ribs, spine, and loin area behind the last rib.

**Score 1 — Emaciated**: Spine and ribs sharp, no fat cover. Requires immediate nutritional intervention.
**Score 2 — Thin**: Ribs easily felt, spine prominent. Increase feed quality/quantity.
**Score 3 — Ideal**: Ribs felt with slight pressure, smooth spine. Maintain current feeding.
**Score 4 — Fat**: Ribs hard to feel, spine rounded over. Reduce grain, increase exercise.
**Score 5 — Obese**: Cannot feel ribs, thick fat deposits. Significant diet change needed.

**Ideal BCS by Stage**
- Dry does: 3.0-3.5
- Late pregnancy: 3.0-3.5 (don't let them get too fat — pregnancy toxemia risk)
- Early lactation: 2.5-3.0 (some weight loss normal)
- Bucks in rut: May drop to 2.0-2.5 (normal, recover after)
- Growing kids: 3.0

**Tips**
- Score monthly and record in GoatLab
- Hair coat can hide condition — always feel, don't just look
- Sudden changes indicate health issues" },

            new CareArticle { Id = 10, Title = "Hoof Trimming Guide", Category = CareArticleCategory.HealthAndCare, SortOrder = 5, IsBuiltIn = true,
                Summary = "Step-by-step hoof care and trimming schedule.",
                Content = @"Regular hoof care prevents lameness, foot rot, and pain.

**When to Trim**
- Every 6-8 weeks (more often on soft ground, less on rocky terrain)
- Any time hooves look overgrown or curled
- Before shows or sales

**Tools Needed**
- Sharp hoof shears or trimmers
- Hoof knife (for detailed work)
- Blood stop powder (in case of over-trimming)
- Gloves
- Milk stand or trimming stand

**Step-by-Step**
1. Secure the goat on a stand or have a helper hold
2. Clean out dirt and debris with the tip of the shears
3. Trim the overgrown wall — cut parallel to the growth rings
4. Trim the heel to be even with the sole
5. Flatten the sole — it should be flat, not cupped
6. The goal: the bottom of the hoof looks like a flat, pink triangle
7. If you see pink, stop — you're close to the quick
8. If you draw blood, apply blood stop powder and don't panic

**Hoof Health**
- Keep bedding dry (wet = foot rot)
- Zinc sulfate foot baths for prevention
- Treat foot rot immediately (trim + topical treatment)
- Biotin supplements can improve hoof quality" },

            new CareArticle { Id = 11, Title = "Emergency First Aid Kit", Category = CareArticleCategory.HealthAndCare, SortOrder = 6, IsBuiltIn = true,
                Summary = "Essential supplies every goat owner should have on hand.",
                Content = @"Be prepared for emergencies. Stock these supplies:

**Must-Have Supplies**
- Digital rectal thermometer (normal goat temp: 101.5-103.5°F)
- Syringes (3cc, 6cc, 12cc) and needles (18-20 gauge)
- CDT vaccine
- Dewormer (at least 2 different classes)
- Electrolyte powder or Gatorade
- Pepto-Bismol or kaolin-pectin (for scours)
- Iodine or Betadine (for wound/navel care)
- Blood stop powder
- Activated charcoal (for poisoning)
- Baking soda (for bloat)
- Vegetable/mineral oil (for bloat)
- Probiotics paste
- Nutridrench or CMPK (for pregnancy toxemia/milk fever)
- Banamine (prescription — ask your vet)
- Penicillin or LA-200 (prescription)

**Kidding Kit Additions**
- OB lube
- OB gloves (shoulder length)
- Bulb syringe (to clear kid's airway)
- Dental floss or umbilical clamps (for cords)
- Iodine (7% for navel dipping)
- Towels
- Heat lamp or hair dryer
- Colostrum replacer (frozen colostrum is better)
- Bottle and Pritchard nipple

**When to Call the Vet**
- Temperature over 104°F or under 100°F
- Labored breathing
- Inability to stand
- Bloat not responding to treatment
- Kidding difficulties (no progress after 30 min of active labor)
- Severe bleeding or injury" },

            // Breeding (12-15)
            new CareArticle { Id = 12, Title = "Breeding Basics", Category = CareArticleCategory.Breeding, SortOrder = 1, IsBuiltIn = true,
                Summary = "When and how to breed goats, buck-to-doe ratios, and breeding season.",
                Content = @"**Breeding Season**
Most goat breeds are seasonal breeders (fall/winter). Some breeds like Nigerian Dwarf can breed year-round.
- Typical season: August through February
- Triggered by decreasing daylight hours
- Bucks become more odorous and aggressive during rut

**Age & Maturity**
- Does: Breed at 7-10 months or when they reach 60-70% of adult weight
- Bucks: Fertile by 4-5 months (separate early!)
- Don't breed too young — stunts growth and increases complications

**Buck-to-Doe Ratios**
- 1 buck per 25-30 does (mature buck)
- 1 young buck per 10-15 does
- Keep backup bucks when possible

**Heat Cycle**
- Cycle length: 18-24 days (average 21 days)
- Standing heat lasts: 12-36 hours
- Signs: tail wagging, vocalization, swollen/red vulva, mounting other does, decreased appetite
- Breed during standing heat for best results

**Methods**
- Pen breeding: Put buck with doe(s) for 2-3 heat cycles
- Hand breeding: Supervised single mating, record exact date
- AI (Artificial Insemination): Requires training and equipment" },

            new CareArticle { Id = 13, Title = "Gestation & Pregnancy Care", Category = CareArticleCategory.Breeding, SortOrder = 2, IsBuiltIn = true,
                Summary = "What to expect during the 150-day gestation period.",
                Content = @"Goat gestation averages **150 days** (145-155 day range).

**Pregnancy Timeline**
- **Day 1-30**: Embryo implantation. Avoid stress and handling.
- **Day 30-90**: Fetal development. Maintain normal diet.
- **Day 90-120**: Rapid fetal growth. Gradually increase nutrition.
- **Day 120-150**: Final growth. Increase grain, supplement selenium/vitamin E.

**Nutrition**
- First 3 months: Good hay and minerals are sufficient
- Last 6 weeks: Increase grain gradually (up to 1 lb/day)
- Provide selenium/vitamin E supplement (if deficient in your area)
- Free-choice loose minerals always available
- Fresh clean water — pregnant does drink more

**CDT Vaccination**
Booster 4-6 weeks before due date. This passes immunity to kids through colostrum.

**Warning Signs**
- Vaginal discharge (clear mucus near term is normal; colored discharge is not)
- Loss of appetite for more than 24 hours
- Grinding teeth (pain)
- Swelling in legs/udder is normal near term
- Lying down and not getting up — check for pregnancy toxemia

**Pregnancy Toxemia Prevention**
- Don't let does get too fat OR too thin
- Ensure adequate nutrition in last 6 weeks
- Reduce stress (don't transport, change housing, etc.)
- Does carrying multiples are at higher risk" },

            new CareArticle { Id = 14, Title = "Kidding: What to Expect", Category = CareArticleCategory.Breeding, SortOrder = 3, IsBuiltIn = true,
                Summary = "Signs of labor, normal delivery, and when to intervene.",
                Content = @"**Pre-Labor Signs (Days Before)**
- Ligaments around tail head soften/disappear
- Udder fills and becomes tight
- Vulva swells and elongates
- Doe becomes restless, paws at ground
- May separate from herd

**Active Labor**
- **Stage 1** (2-12 hours): Contractions begin, doe is restless, may talk to her belly
- **Stage 2** (30 min - 2 hours): Active pushing, water bag appears, kid delivered
- **Stage 3** (up to 12 hours): Placenta passed

**Normal Presentations**
- Front feet first with nose resting on legs (diving position) — ideal
- Both front feet with head — normal
- Rear feet first (breech) — assist gently

**When to Intervene**
- Hard pushing for 30+ minutes with no progress
- Only one foot visible (other leg may be back)
- Head visible but no feet
- Kid stuck at shoulders or hips
- Doe exhausted and stopped pushing

**Newborn Kid Care**
1. Clear airway — remove mucus from nose and mouth
2. Stimulate breathing — rub vigorously with towel
3. Dip navel in 7% iodine
4. Ensure kid nurses within 1-2 hours (colostrum is critical)
5. Watch for hypothermia — dry kid and provide warmth if needed" },

            new CareArticle { Id = 15, Title = "Raising Kids (Baby Goats)", Category = CareArticleCategory.Breeding, SortOrder = 4, IsBuiltIn = true,
                Summary = "Dam-raised vs bottle-fed, weaning, and kid health.",
                Content = @"**Dam-Raised vs Bottle-Fed**
- **Dam-raised**: Less work, natural bonding, doe handles feeding. Best for meat breeds.
- **Bottle-fed**: Friendlier kids, control over milk amount, necessary if doe rejects kid or has mastitis.

**Bottle Feeding Schedule**
- Day 1-3: Colostrum only, 2-4 oz every 2-4 hours
- Week 1-2: 4-6 oz, 4 times daily
- Week 3-4: 8-12 oz, 3 times daily
- Week 5-8: 12-16 oz, 2 times daily
- Wean at 8-12 weeks

**Key Milestones**
- Start offering hay and a little grain at 1-2 weeks
- Disbud (if desired) at 3-10 days (breed dependent)
- CDT vaccine at 4-6 weeks, booster at 8-10 weeks
- Coccidia prevention starting at 3-4 weeks
- Wean when eating well and at least 2-2.5x birth weight

**Common Kid Issues**
- Floppy Kid Syndrome: Weak, can't stand. Often responds to baking soda and electrolytes.
- Hypothermia: Warm gradually, feed warm colostrum
- Scours (diarrhea): Electrolytes, reduce milk, treat cause
- Naval ill: Prevent by dipping navel in iodine at birth" },

            // Daily Management (16-19)
            new CareArticle { Id = 16, Title = "Daily Feeding Guide", Category = CareArticleCategory.DailyManagement, SortOrder = 1, IsBuiltIn = true,
                Summary = "What to feed, how much, and feeding schedules.",
                Content = @"Proper nutrition is the foundation of a healthy herd.

**Hay (Foundation of the Diet)**
- 2-4 lbs per goat per day (about 3-5% of body weight)
- Grass hay: Good maintenance diet
- Alfalfa: Higher protein, great for lactating does and growing kids
- Mixed grass/alfalfa: Good all-purpose option
- Always provide free-choice hay

**Grain (Supplemental)**
- Lactating does: 1-2 lbs/day depending on production
- Late pregnancy: 0.5-1 lb/day
- Bucks in rut: 0.5-1 lb/day
- Maintenance (dry does, wethers): Little to no grain needed
- Introduce and change grain gradually over 7-10 days

**Minerals**
- Loose goat-specific minerals (NOT sheep minerals — goats need copper)
- Free-choice, always available
- Baking soda free-choice (helps with rumen pH)

**Water**
- 2-4 gallons per goat per day
- More in summer and during lactation
- Clean and fresh — goats are picky

**Treats (in moderation)**
- Fruit, vegetables, bread, animal crackers
- Avoid: chocolate, avocado, wild cherry, rhododendron, azalea" },

            new CareArticle { Id = 17, Title = "Milking Basics", Category = CareArticleCategory.DailyManagement, SortOrder = 2, IsBuiltIn = true,
                Summary = "How to hand milk, equipment, and milk handling.",
                Content = @"**Getting Started**
- Does need to freshen (give birth) before they produce milk
- First 3-5 days: Colostrum for kids only
- Begin milking once kids are old enough to share or are weaned

**Equipment**
- Milk stand with head catch
- Stainless steel or food-grade bucket
- Teat dip or udder wash
- Milk strainer and filters
- Glass jars for storage

**Milking Procedure**
1. Secure doe on milk stand with grain
2. Wash udder with warm water or udder wash
3. Strip first few squirts into a strip cup (check for clots/blood)
4. Milk with full-hand squeeze: trap milk with thumb and forefinger, squeeze down with remaining fingers
5. Alternate hands rhythmically
6. Milk until udder feels soft and flat
7. Apply teat dip after milking
8. Strain milk immediately and chill to 40°F within an hour

**Milk Production**
- Nigerian Dwarf: 1-3 lbs/day
- Standard dairy breeds: 6-12 lbs/day
- Peak production: 4-8 weeks after kidding
- Lactation length: 10-12 months

**Schedule**
- Milk every 12 hours for best production
- Consistency is key — same time each day
- Once daily milking is possible with reduced yield" },

            new CareArticle { Id = 18, Title = "Seasonal Farm Checklists", Category = CareArticleCategory.DailyManagement, SortOrder = 3, IsBuiltIn = true,
                Summary = "Month-by-month and seasonal task guides.",
                Content = @"**Spring**
- Deworm based on FAMACHA scores (parasite season begins)
- Begin rotational grazing
- CDT boosters for pregnant does (4-6 weeks before kidding)
- Prepare kidding stalls
- Start coccidia prevention for kids
- Hoof trimming
- Check/repair fencing after winter

**Summer**
- Provide shade and ample water
- Monitor for heat stress (panting, lethargy)
- FAMACHA checks every 2-3 weeks
- Fly control (fly traps, sprays)
- Maintain pasture rotation
- Trim hooves

**Fall**
- Breeding season begins
- Put bucks with does (record dates!)
- Annual CDT vaccination
- Increase nutrition for bred does
- Stock up on hay for winter
- Trim hooves
- Prepare shelters for winter

**Winter**
- Ensure unfrozen water (heated buckets)
- Increase hay for cold weather calories
- Check shelter for drafts (ventilation without drafts)
- Kidding season (if fall-bred)
- Monitor body condition — increase feed if needed
- Reduce hoof trimming frequency
- Plan next year's breeding" },

            new CareArticle { Id = 19, Title = "Pasture Management", Category = CareArticleCategory.DailyManagement, SortOrder = 4, IsBuiltIn = true,
                Summary = "Rotational grazing, parasite control, and forage management.",
                Content = @"Good pasture management reduces parasites and improves herd health.

**Rotational Grazing Basics**
- Divide pasture into 3-4+ paddocks
- Graze each paddock for 5-7 days
- Rest each paddock for 4-6 weeks minimum
- Move animals when forage is grazed to 3-4 inches
- Never overgraze — it weakens plants and increases parasites

**Parasite Control Through Grazing**
- Most larvae are in the bottom 2 inches of forage
- Don't graze below 4 inches
- Sun and heat kill larvae — rest during summer is more effective
- Multi-species grazing (cattle/horses with goats) breaks parasite cycles
- Mow after grazing to expose larvae to sunlight

**Stocking Rates**
- General guideline: 6-8 goats per acre of good pasture
- Depends on forage quality, rainfall, and supplemental feeding
- Overstocking = more parasites, less forage, poor condition

**Improving Pastures**
- Soil test every 2-3 years
- Lime and fertilize based on test results
- Overseed thin areas
- Goats prefer browse (shrubs, weeds, brush) over grass
- Planting browse species (willows, mulberry) can supplement grazing

**Pasture Condition Scoring**
Rate pastures 1-5 based on forage density, diversity, weed pressure, and ground cover. Track in GoatLab to optimize rotation timing." },

            // Production (20-22)
            new CareArticle { Id = 20, Title = "Tracking Milk Production", Category = CareArticleCategory.Production, SortOrder = 1, IsBuiltIn = true,
                Summary = "Why and how to track daily milk yield per goat.",
                Content = @"Tracking milk production helps you make breeding, feeding, and culling decisions.

**What to Track**
- Daily yield per goat (weigh milk in lbs or measure in cups)
- AM vs PM if milking twice daily
- Milk quality notes (off taste, color changes, clots)
- Days in milk (DIM) — how many days since kidding

**Why Track**
- Identify top producers for breeding
- Detect mastitis early (sudden drop in production)
- Optimize feeding (high producers need more grain)
- Plan dry-off timing (when to stop milking before next kidding)
- Calculate cost-per-gallon and profitability

**Typical Lactation Curve**
- Weeks 1-2: Production ramps up
- Weeks 4-8: Peak production
- Months 3-10: Gradual decline
- Month 10-12: Dry off (stop milking 2 months before next kidding)

**Using GoatLab**
Log milk daily from the Dashboard quick-entry or the Milk Production page. View trend charts to spot patterns and compare does." },

            new CareArticle { Id = 21, Title = "Selling Goats & Products", Category = CareArticleCategory.Production, SortOrder = 2, IsBuiltIn = true,
                Summary = "Tips for marketing and selling goats, milk, and meat.",
                Content = @"**Selling Live Animals**
- Build a reputation through quality animals and honest descriptions
- Take good photos (side profile, udder for does, muscling for bucks)
- Provide health records and registration papers
- Price based on breed, quality, age, and your market
- Use GoatLab's sales tracking and customer CRM features

**Selling Milk & Dairy Products**
- Know your state's raw milk laws before selling
- Grade A dairy requires licensed facilities in most states
- Goat milk soap is often legal without dairy licensing
- Cheese, yogurt, etc. typically require food processing licenses

**Selling Meat**
- USDA inspection required for retail sales in most cases
- Custom slaughter (buyer purchases live animal, pays for processing) avoids some regulations
- Halal and ethnic markets can be strong demand
- Track hanging weight and packaged weight for pricing

**Marketing Channels**
- Farm website and social media
- Local farmers markets
- Craigslist / Facebook Marketplace
- Breed association classified ads
- Word of mouth — your best customers refer others" },

            new CareArticle { Id = 22, Title = "Financial Record Keeping", Category = CareArticleCategory.Production, SortOrder = 3, IsBuiltIn = true,
                Summary = "Track income, expenses, and calculate cost-per-goat.",
                Content = @"Good records help you make smart farm decisions and simplify taxes.

**What to Track**
- All income: animal sales, milk sales, breeding fees
- All expenses: feed, hay, vet bills, supplies, equipment
- Assign costs to specific goats when possible (for cost analysis)

**Key Metrics**
- **Cost per goat per month**: Total expenses / number of goats / months
- **Cost per gallon of milk**: (Feed + supplies + labor) / gallons produced
- **Break-even price**: Total annual costs / number of animals sold
- **Return per doe**: Income from doe (milk + kids) minus her costs

**Tax Considerations**
- Farm income/loss reported on Schedule F
- Keep receipts for all farm purchases
- Mileage to/from farm supply stores, vet, etc.
- Equipment depreciation
- Consult a tax professional familiar with agricultural exemptions

**Using GoatLab**
Log every transaction in the Finance section. Use the cost-per-goat analysis to identify which animals are profitable and which are costing you money. Export CSV reports for your accountant." },

            // Reference & Tools (23-26)
            new CareArticle { Id = 23, Title = "Common Goat Breeds Reference", Category = CareArticleCategory.ReferenceAndTools, SortOrder = 1, IsBuiltIn = true,
                Summary = "Quick reference for popular dairy, meat, and fiber breeds.",
                Content = @"**Dairy Breeds**
- **Nubian**: Roman nose, long ears, rich milk (high butterfat). Loud.
- **Alpine**: Hardy, high producers, many color patterns.
- **Saanen**: White, highest volume producers, gentle.
- **LaMancha**: Very short ears, friendly, good milk quality.
- **Nigerian Dwarf**: Small (under 75 lbs), highest butterfat, breed year-round.
- **Oberhasli**: Bay colored, moderate production, quiet.
- **Toggenburg**: Swiss breed, oldest registered dairy breed, moderate production.

**Meat Breeds**
- **Boer**: Large, white body/red head, fast growth, excellent muscling.
- **Kiko**: Hardy, parasite resistant, good mothers, NZ origin.
- **Spanish**: Hardy range goats, lean meat, excellent foragers.
- **Savanna**: White, heat tolerant, good mothers, from South Africa.
- **Myotonic (Fainting)**: Muscle condition causes stiffness, heavily muscled.

**Fiber Breeds**
- **Angora**: Produces mohair, require shearing twice yearly.
- **Cashmere**: Fine undercoat harvested annually, any breed can produce.

**Miniature Breeds**
- **Pygmy**: Stocky, compact, primarily pets/companions. 60-80 lbs.
- **Mini breeds**: Crosses of Nigerian Dwarf with standard dairy breeds." },

            new CareArticle { Id = 24, Title = "Normal Vital Signs & Reference Numbers", Category = CareArticleCategory.ReferenceAndTools, SortOrder = 2, IsBuiltIn = true,
                Summary = "Quick-reference vital signs, weights, and production numbers.",
                Content = @"**Vital Signs**
- Temperature: 101.5-103.5°F (rectal)
- Heart rate: 70-90 beats/min (adult), 100-120 (kids)
- Respiration: 12-25 breaths/min
- Rumen contractions: 1-2 per minute

**Reproduction**
- Heat cycle: 18-24 days (avg 21)
- Gestation: 145-155 days (avg 150)
- Breeding age: 7-10 months (60-70% adult weight)
- Kids per birth: 1-4 (twins most common)

**Weight Ranges (Adult)**
- Nigerian Dwarf: 50-75 lbs
- Pygmy: 60-80 lbs
- Alpine/Saanen/Nubian: 130-200 lbs
- Boer: 200-340 lbs

**Milk Production (Daily Average)**
- Nigerian Dwarf: 1-3 lbs
- Nubian: 4-8 lbs
- Alpine/Saanen: 6-12 lbs
- LaMancha: 5-9 lbs

**Feed Requirements**
- Hay: 3-5% of body weight daily
- Water: 2-4 gallons daily (more in heat/lactation)
- Grain: 0-2 lbs/day depending on stage

**Lifespan**: 10-15 years (does), 8-12 years (bucks)" },

            new CareArticle { Id = 25, Title = "Poisonous Plants for Goats", Category = CareArticleCategory.ReferenceAndTools, SortOrder = 3, IsBuiltIn = true,
                Summary = "Plants to keep away from your goats.",
                Content = @"Goats are browsers and will sample many plants. Most are fine, but some are toxic.

**Highly Toxic (Can Be Fatal)**
- Azalea / Rhododendron
- Yew (all parts)
- Oleander
- Water hemlock
- Poison hemlock
- Cherry (wilted leaves — fresh and dry are OK)
- Mountain laurel
- Lily of the valley

**Moderately Toxic (Illness)**
- Rhubarb leaves
- Raw potatoes (green parts)
- Nightshade family
- Bracken fern
- Jimsonweed
- Milkweed
- Oak (excess acorns)

**Generally Safe Plants Goats Love**
- Multiflora rose
- Honeysuckle
- Blackberry/raspberry brambles
- Kudzu
- Clover
- Chicory
- Plantain (the weed, not the banana)
- Willow
- Mulberry

**Prevention**
- Walk your pastures and identify plants before introducing goats
- Remove or fence off toxic plants
- Well-fed goats are less likely to eat toxic plants
- Provide free-choice baking soda (helps with mild toxin ingestion)" },

            new CareArticle { Id = 26, Title = "Glossary of Goat Terms", Category = CareArticleCategory.ReferenceAndTools, SortOrder = 4, IsBuiltIn = true,
                Summary = "Common terminology used in goat farming.",
                Content = @"**Animal Terms**
- **Doe / Nanny**: Adult female goat
- **Buck / Billy**: Adult male goat (intact)
- **Wether**: Castrated male goat
- **Doeling**: Young female (under 1 year)
- **Buckling**: Young male (under 1 year)
- **Kid**: Baby goat of either gender
- **Yearling**: Goat between 1-2 years old

**Breeding Terms**
- **Freshen**: To give birth and begin producing milk
- **Dry**: Not currently producing milk
- **In kid**: Pregnant
- **Kidding**: Giving birth
- **Dam**: Mother
- **Sire**: Father
- **Rut**: Buck breeding season (increased hormones, odor)
- **Standing heat**: When doe is receptive to breeding

**Health Terms**
- **FAMACHA**: Eyelid color chart for anemia detection
- **BCS**: Body Condition Score (1-5 fat assessment)
- **CDT**: Core vaccine (Clostridium + Tetanus)
- **Scours**: Diarrhea
- **Bloat**: Rumen gas buildup (emergency)
- **Ketosis/Pregnancy toxemia**: Metabolic disease in late pregnancy
- **CAE**: Caprine Arthritis Encephalitis (viral disease)
- **CL**: Caseous Lymphadenitis (abscesses)
- **Mastitis**: Udder infection

**Production Terms**
- **Butterfat**: Fat content of milk (4-10% in goats)
- **DIM**: Days in Milk (since last kidding)
- **Dry off**: Stopping milking to rest the doe before next kidding
- **Colostrum**: First milk after birth, rich in antibodies" }
        );
    }

    private static void SeedMedications(ModelBuilder mb)
    {
        mb.Entity<Medication>().HasData(
            new Medication { Id = 1, Name = "CDT Vaccine", Description = "Clostridium Perfringens Types C&D + Tetanus. Core vaccine for all goats.", Route = "SubQ", DosageRate = "2 mL per goat regardless of size", DosagePerPound = null, Notes = "Annual booster. Pregnant does: 4-6 weeks before kidding. Kids: 4-6 weeks then booster at 8-10 weeks." },
            new Medication { Id = 2, Name = "Ivermectin (Ivomec)", Description = "Broad-spectrum dewormer effective against roundworms, lungworms, and external parasites.", Route = "Oral or SubQ", DosageRate = "1 mL per 50 lbs (oral, cattle formulation)", DosagePerPound = 0.02, MeatWithdrawalDays = 35, MilkWithdrawalDays = 9, Notes = "Give orally for goats — higher bioavailability than injection." },
            new Medication { Id = 3, Name = "Fenbendazole (SafeGuard)", Description = "Dewormer effective against roundworms, some tapeworms.", Route = "Oral", DosageRate = "1 mL per 5 lbs (10% liquid suspension)", DosagePerPound = 0.2, MeatWithdrawalDays = 16, MilkWithdrawalDays = 4, Notes = "Double or triple the cattle dose for goats. 3-day treatment for Meningeal worm." },
            new Medication { Id = 4, Name = "Albendazole (Valbazen)", Description = "Broad-spectrum dewormer including liver flukes and tapeworms.", Route = "Oral", DosageRate = "1 mL per 25 lbs", DosagePerPound = 0.04, MeatWithdrawalDays = 27, MilkWithdrawalDays = 7, Notes = "Do NOT use in first 30 days of pregnancy — can cause birth defects." },
            new Medication { Id = 5, Name = "Penicillin G Procaine", Description = "Antibiotic for respiratory infections, foot rot, wound infections.", Route = "SubQ or IM", DosageRate = "1 mL per 20 lbs twice daily for 5 days", DosagePerPound = 0.05, MeatWithdrawalDays = 30, MilkWithdrawalDays = 4, Notes = "Refrigerate. Give for full course — don't stop early." },
            new Medication { Id = 6, Name = "LA-200 (Oxytetracycline)", Description = "Long-acting antibiotic for respiratory and systemic infections.", Route = "SubQ or IM", DosageRate = "1 mL per 20 lbs every 48-72 hours", DosagePerPound = 0.05, MeatWithdrawalDays = 28, MilkWithdrawalDays = 7, Notes = "Can cause tissue irritation at injection site. Rotate sites." },
            new Medication { Id = 7, Name = "Banamine (Flunixin)", Description = "NSAID pain reliever and anti-inflammatory. Prescription required.", Route = "IV or Oral", DosageRate = "1 mL per 100 lbs", DosagePerPound = 0.01, MeatWithdrawalDays = 30, MilkWithdrawalDays = 4, Notes = "Never give IM — causes tissue necrosis. IV preferred, oral paste also effective." },
            new Medication { Id = 8, Name = "Corid (Amprolium)", Description = "Treatment and prevention of coccidiosis, especially in kids.", Route = "Oral (drench or in water)", DosageRate = "Treatment: 10 mL per 25 lbs of 9.6% solution for 5 days", DosagePerPound = 0.4, MeatWithdrawalDays = 0, MilkWithdrawalDays = 0, Notes = "Prevention dose is half the treatment dose. Treat for full 5 days." },
            new Medication { Id = 9, Name = "Nutridrench", Description = "Energy supplement for weak, ketotic, or post-kidding does and kids.", Route = "Oral", DosageRate = "Adults: 2 oz. Kids: 1/2 oz", DosagePerPound = null, Notes = "Keep on hand for emergencies. Provides quick energy, vitamins, and minerals." },
            new Medication { Id = 10, Name = "Probios (Probiotics)", Description = "Probiotic paste to restore rumen bacteria after illness, stress, or antibiotic use.", Route = "Oral", DosageRate = "5 grams per goat", DosagePerPound = null, Notes = "Give after antibiotic treatment, during diet changes, or after stressful events." }
        );
    }

    private static void SeedChecklists(ModelBuilder mb)
    {
        mb.Entity<Checklist>().HasData(
            new Checklist { Id = 1, Title = "Morning Chores", Period = TaskPeriod.Morning },
            new Checklist { Id = 2, Title = "Afternoon Chores", Period = TaskPeriod.Afternoon },
            new Checklist { Id = 3, Title = "Evening Chores", Period = TaskPeriod.Evening }
        );

        mb.Entity<ChecklistItem>().HasData(
            // Morning
            new ChecklistItem { Id = 1, ChecklistId = 1, Description = "Check all goats — head count and visual health check", SortOrder = 1 },
            new ChecklistItem { Id = 2, ChecklistId = 1, Description = "Feed hay", SortOrder = 2 },
            new ChecklistItem { Id = 3, ChecklistId = 1, Description = "Feed grain to milking does and kids", SortOrder = 3 },
            new ChecklistItem { Id = 4, ChecklistId = 1, Description = "Refresh water buckets/troughs", SortOrder = 4 },
            new ChecklistItem { Id = 5, ChecklistId = 1, Description = "Milk does", SortOrder = 5 },
            new ChecklistItem { Id = 6, ChecklistId = 1, Description = "Check mineral feeders", SortOrder = 6 },
            // Afternoon
            new ChecklistItem { Id = 7, ChecklistId = 2, Description = "Visual herd check", SortOrder = 1 },
            new ChecklistItem { Id = 8, ChecklistId = 2, Description = "Top off water", SortOrder = 2 },
            new ChecklistItem { Id = 9, ChecklistId = 2, Description = "Check fencing (rotate sections daily)", SortOrder = 3 },
            new ChecklistItem { Id = 10, ChecklistId = 2, Description = "Clean kidding stalls if in use", SortOrder = 4 },
            // Evening
            new ChecklistItem { Id = 11, ChecklistId = 3, Description = "Feed hay (second feeding)", SortOrder = 1 },
            new ChecklistItem { Id = 12, ChecklistId = 3, Description = "Evening milking", SortOrder = 2 },
            new ChecklistItem { Id = 13, ChecklistId = 3, Description = "Lock up barn/shelter (predator protection)", SortOrder = 3 },
            new ChecklistItem { Id = 14, ChecklistId = 3, Description = "Final head count", SortOrder = 4 }
        );
    }
}
