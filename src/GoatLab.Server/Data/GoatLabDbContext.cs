using GoatLab.Server.Data.Auth;
using GoatLab.Server.Services;
using GoatLab.Shared.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Data;

public class GoatLabDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantContext? _tenantContext;

    public GoatLabDbContext(DbContextOptions<GoatLabDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();
    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    // Billing plans (system-wide, admin-managed)
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();

    // Herd
    public DbSet<Goat> Goats => Set<Goat>();
    public DbSet<GoatPhoto> GoatPhotos => Set<GoatPhoto>();
    public DbSet<GoatDocument> GoatDocuments => Set<GoatDocument>();
    public DbSet<Barn> Barns => Set<Barn>();
    public DbSet<Pen> Pens => Set<Pen>();

    // Health & Medical
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<MedicineCabinetItem> MedicineCabinetItems => Set<MedicineCabinetItem>();
    public DbSet<WeightRecord> WeightRecords => Set<WeightRecord>();
    public DbSet<FamachaScore> FamachaScores => Set<FamachaScore>();
    public DbSet<BodyConditionScore> BodyConditionScores => Set<BodyConditionScore>();

    // Breeding & Kidding
    public DbSet<BreedingRecord> BreedingRecords => Set<BreedingRecord>();
    public DbSet<KiddingRecord> KiddingRecords => Set<KiddingRecord>();
    public DbSet<Kid> Kids => Set<Kid>();
    public DbSet<HeatDetection> HeatDetections => Set<HeatDetection>();

    // Production & Sales
    public DbSet<MilkLog> MilkLogs => Set<MilkLog>();
    public DbSet<Lactation> Lactations => Set<Lactation>();
    public DbSet<MilkTestDay> MilkTestDays => Set<MilkTestDay>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<HarvestRecord> HarvestRecords => Set<HarvestRecord>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    // Mapping & Grazing
    public DbSet<Pasture> Pastures => Set<Pasture>();
    public DbSet<PastureConditionLog> PastureConditionLogs => Set<PastureConditionLog>();
    public DbSet<PastureRotation> PastureRotations => Set<PastureRotation>();
    public DbSet<MapMarker> MapMarkers => Set<MapMarker>();
    public DbSet<GrazingArea> GrazingAreas => Set<GrazingArea>();

    // Care Guide
    public DbSet<CareArticle> CareArticles => Set<CareArticle>();

    // Suppliers & Inventory
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<FeedInventory> FeedInventory => Set<FeedInventory>();

    // Shows & Appraisals
    public DbSet<ShowRecord> ShowRecords => Set<ShowRecord>();
    public DbSet<LinearAppraisal> LinearAppraisals => Set<LinearAppraisal>();

    // Calendar & Tasks
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<EventCompletion> EventCompletions => Set<EventCompletion>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<ChecklistCompletion> ChecklistCompletions => Set<ChecklistCompletion>();

    // Vaccination protocols
    public DbSet<VaccinationProtocol> VaccinationProtocols => Set<VaccinationProtocol>();
    public DbSet<ProtocolDose> ProtocolDoses => Set<ProtocolDose>();

    // Settings
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    // Admin audit log (cross-tenant)
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();

    // Announcements (cross-tenant broadcast)
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<AnnouncementDismissal> AnnouncementDismissals => Set<AnnouncementDismissal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Goat self-referencing pedigree relationships.
        // SQL Server rejects multiple cascade paths on self-referencing FKs, so we
        // use ClientSetNull: EF nulls the FK in-memory on parent delete, and the
        // constraint is declared ON DELETE NO ACTION at the database level.
        modelBuilder.Entity<Goat>()
            .HasOne(g => g.Sire)
            .WithMany(g => g.OffspringAsSire)
            .HasForeignKey(g => g.SireId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        modelBuilder.Entity<Goat>()
            .HasOne(g => g.Dam)
            .WithMany(g => g.OffspringAsDam)
            .HasForeignKey(g => g.DamId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // Breeding: Doe and Buck are both goats — prevent cascade cycles
        modelBuilder.Entity<BreedingRecord>()
            .HasOne(b => b.Doe)
            .WithMany()
            .HasForeignKey(b => b.DoeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BreedingRecord>()
            .HasOne(b => b.Buck)
            .WithMany()
            .HasForeignKey(b => b.BuckId)
            .OnDelete(DeleteBehavior.SetNull);

        // KiddingRecord -> Kid goat (optional)
        modelBuilder.Entity<KiddingRecord>()
            .HasOne(k => k.KidGoat)
            .WithMany()
            .HasForeignKey(k => k.KidGoatId)
            .OnDelete(DeleteBehavior.SetNull);

        // Kid -> KiddingRecord (cascade) and Kid -> LinkedGoat (optional)
        modelBuilder.Entity<Kid>()
            .HasOne(k => k.KiddingRecord)
            .WithMany(kr => kr.Kids)
            .HasForeignKey(k => k.KiddingRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Kid>()
            .HasOne(k => k.LinkedGoat)
            .WithMany()
            .HasForeignKey(k => k.LinkedGoatId)
            .OnDelete(DeleteBehavior.SetNull);

        // Sale -> Goat (optional)
        modelBuilder.Entity<Sale>()
            .HasOne(s => s.Goat)
            .WithMany()
            .HasForeignKey(s => s.GoatId)
            .OnDelete(DeleteBehavior.SetNull);

        // Transaction -> Goat (optional, for cost-per-goat)
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.Goat)
            .WithMany()
            .HasForeignKey(t => t.GoatId)
            .OnDelete(DeleteBehavior.SetNull);

        // HarvestRecord -> Goat (optional)
        modelBuilder.Entity<HarvestRecord>()
            .HasOne(h => h.Goat)
            .WithMany()
            .HasForeignKey(h => h.GoatId)
            .OnDelete(DeleteBehavior.SetNull);

        // CalendarEvent -> Goat (optional)
        modelBuilder.Entity<CalendarEvent>()
            .HasOne(c => c.Goat)
            .WithMany()
            .HasForeignKey(c => c.GoatId)
            .OnDelete(DeleteBehavior.SetNull);

        // Purchase -> Goat / Supplier (set-null on delete)
        modelBuilder.Entity<Purchase>()
            .HasOne(p => p.Goat)
            .WithMany()
            .HasForeignKey(p => p.GoatId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Purchase>()
            .HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        // EventCompletion -> CalendarEvent (cascade)
        modelBuilder.Entity<EventCompletion>()
            .HasOne(c => c.CalendarEvent)
            .WithMany(e => e.Completions)
            .HasForeignKey(c => c.CalendarEventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EventCompletion>()
            .HasIndex(c => new { c.CalendarEventId, c.OccurrenceDate })
            .IsUnique();

        // ProtocolDose -> Protocol (cascade); -> Medication (set null)
        modelBuilder.Entity<ProtocolDose>()
            .HasOne(d => d.VaccinationProtocol)
            .WithMany(p => p.Doses)
            .HasForeignKey(d => d.VaccinationProtocolId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProtocolDose>()
            .HasOne(d => d.Medication)
            .WithMany()
            .HasForeignKey(d => d.MedicationId)
            .OnDelete(DeleteBehavior.SetNull);

        // Lactation -> Goat (cascade); TestDay -> Lactation (cascade)
        modelBuilder.Entity<Lactation>()
            .HasOne(l => l.Goat)
            .WithMany()
            .HasForeignKey(l => l.GoatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Lactation>()
            .HasOne(l => l.KiddingRecord)
            .WithMany()
            .HasForeignKey(l => l.KiddingRecordId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MilkTestDay>()
            .HasOne(t => t.Lactation)
            .WithMany(l => l.TestDays)
            .HasForeignKey(t => t.LactationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Lactation>().HasIndex(l => new { l.GoatId, l.FreshenDate });
        modelBuilder.Entity<MilkTestDay>().HasIndex(t => t.TestDate);

        // Shows / Appraisals — cascade-delete with goat
        modelBuilder.Entity<ShowRecord>()
            .HasOne(s => s.Goat)
            .WithMany()
            .HasForeignKey(s => s.GoatId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LinearAppraisal>()
            .HasOne(l => l.Goat)
            .WithMany()
            .HasForeignKey(l => l.GoatId)
            .OnDelete(DeleteBehavior.Cascade);

        // AppSettings unique key
        modelBuilder.Entity<AppSetting>()
            .HasIndex(s => s.Key)
            .IsUnique();

        // Admin audit log — chronological reads are the common case
        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => a.At);
        modelBuilder.Entity<AdminAuditLog>()
            .HasIndex(a => new { a.TargetType, a.TargetId });

        // Announcements: common queries filter by active + time window + target tag.
        modelBuilder.Entity<Announcement>()
            .HasIndex(a => new { a.IsActive, a.StartsAt, a.EndsAt });
        modelBuilder.Entity<AnnouncementDismissal>()
            .HasIndex(d => new { d.AnnouncementId, d.UserId }).IsUnique();
        modelBuilder.Entity<AnnouncementDismissal>()
            .HasOne(d => d.Announcement)
            .WithMany()
            .HasForeignKey(d => d.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant filter: hide soft-deleted unless BypassFilter is set.
        modelBuilder.Entity<Tenant>().HasQueryFilter(t =>
            _tenantContext == null || _tenantContext.BypassFilter || t.DeletedAt == null);

        // Lookup active invites by token hash + block dup invite per (tenant,email).
        modelBuilder.Entity<TenantInvitation>()
            .HasIndex(i => i.TokenHash).IsUnique();
        modelBuilder.Entity<TenantInvitation>()
            .HasIndex(i => new { i.TenantId, i.Email });

        // Plans
        modelBuilder.Entity<Plan>().HasIndex(p => p.Slug).IsUnique();
        modelBuilder.Entity<PlanFeature>()
            .HasKey(pf => new { pf.PlanId, pf.Feature });
        modelBuilder.Entity<PlanFeature>()
            .HasOne(pf => pf.Plan)
            .WithMany(p => p.Features)
            .HasForeignKey(pf => pf.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tenant -> Plan (restrict delete so a plan with active tenants can't be orphaned)
        modelBuilder.Entity<Tenant>()
            .HasOne(t => t.Plan)
            .WithMany()
            .HasForeignKey(t => t.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes for common queries
        modelBuilder.Entity<Goat>().HasIndex(g => g.Status);
        modelBuilder.Entity<Goat>().HasIndex(g => g.Name);
        modelBuilder.Entity<MedicalRecord>().HasIndex(m => m.NextDueDate);
        modelBuilder.Entity<MilkLog>().HasIndex(m => m.Date);
        modelBuilder.Entity<Transaction>().HasIndex(t => t.Date);
        modelBuilder.Entity<CalendarEvent>().HasIndex(c => c.Start);
        modelBuilder.Entity<ChecklistCompletion>().HasIndex(c => c.Date);

        // Tenant-scoped indexes + relationships
        modelBuilder.Entity<Tenant>().HasIndex(t => t.Slug).IsUnique();
        modelBuilder.Entity<TenantMember>()
            .HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
        modelBuilder.Entity<TenantMember>()
            .HasOne(m => m.Tenant)
            .WithMany(t => t.Members)
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global query filter on every ITenantOwned entity. Reads are scoped to
        // the current tenant automatically; writes still need explicit TenantId
        // stamping in the controller/service layer.
        ApplyTenantFilters(modelBuilder);

        // Seed data
        SeedData.Seed(modelBuilder);
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType)) continue;

            var method = typeof(GoatLabDbContext)
                .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, new object[] { modelBuilder });

            // Index TenantId on every tenant-owned entity so scoped queries are fast.
            modelBuilder.Entity(entityType.ClrType).HasIndex("TenantId");

            // The Tenant FK must NOT cascade. SQL Server rejects multiple cascade
            // paths on a single row, which would happen any time a tenant-owned
            // entity also has a cascade FK to another tenant-owned entity (e.g.
            // Pen -> Barn + Pen -> Tenant). Tenant deletion is a rare, explicit
            // admin operation anyway — we'll handle the cascade in code.
            modelBuilder.Entity(entityType.ClrType)
                .HasOne(nameof(ITenantOwned.Tenant))
                .WithMany()
                .HasForeignKey(nameof(ITenantOwned.TenantId))
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            _tenantContext == null ||
            _tenantContext.BypassFilter ||
            e.TenantId == _tenantContext.TenantId);
    }

    public override int SaveChanges()
    {
        StampTenantId();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTenantId()
    {
        if (_tenantContext?.TenantId is not int tid) return;

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == 0)
                entry.Entity.TenantId = tid;
        }
    }
}
