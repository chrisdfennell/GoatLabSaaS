namespace GoatLab.Shared.Models;

public enum GoatStatus
{
    Healthy,
    Sick,
    AtVet,
    Deceased,
    Sold
}

public enum Gender
{
    Male,
    Female,
    Wether // castrated male
}

public enum MedicalRecordType
{
    Vaccination,
    Deworming,
    Illness,
    Treatment,
    Surgery,
    Checkup,
    Other
}

public enum RecurrenceInterval
{
    None,
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    BiAnnually,
    Annually
}

public enum BreedingOutcome
{
    Pending,
    Confirmed,
    Failed,
    Aborted
}

public enum KiddingOutcome
{
    Healthy,
    Stillborn,
    Complications,
    Died
}

public enum AssistanceLevel
{
    None,
    Light,
    Hard,
    Cesarean
}

public enum KidPresentation
{
    Normal,
    Breech,
    Transverse,
    HeadBack,
    LegBack,
    Other
}

public enum KidVigor
{
    Strong,
    Weak,
    Bottle,
    Died
}

public enum PaymentStatus
{
    Pending,
    Deposited,
    PaidInFull,
    Refunded,
    Cancelled
}

public enum SaleType
{
    LiveAnimal,
    Milk,
    Meat,
    Breeding,
    Other
}

public enum TransactionType
{
    Income,
    Expense
}

public enum ExpenseCategory
{
    Feed,
    Veterinary,
    Equipment,
    Supplies,
    Labor,
    Facility,
    Transport,
    Insurance,
    Other
}

public enum IncomeCategory
{
    AnimalSale,
    MilkSale,
    MeatSale,
    BreedingFee,
    Other
}

public enum MapMarkerType
{
    Barn,
    Shelter,
    Water,
    Feeder,
    Gate,
    Other
}

public enum PastureCondition
{
    Poor = 1,
    Fair = 2,
    Good = 3,
    VeryGood = 4,
    Excellent = 5
}

public enum TaskPeriod
{
    Morning,
    Afternoon,
    Evening,
    AnyTime
}

public enum CareArticleCategory
{
    GettingStarted,
    HealthAndCare,
    Breeding,
    DailyManagement,
    Production,
    ReferenceAndTools
}

public enum SupplierType
{
    FeedSupplier,
    Veterinarian,
    EquipmentVendor,
    Other
}

public enum GoatRegistry
{
    None,
    ADGA,        // American Dairy Goat Association
    AGS,         // American Goat Society
    Myotonic,    // Myotonic Goat Registry
    Kiko,        // American Kiko Goat Association
    Boer,        // American Boer Goat Association
    Savanna,
    PGCH,        // Pedigree International
    Other
}

public enum LinearAppraisalClassification
{
    Excellent,          // 90+
    VeryGood,           // 85-89
    GoodPlus,           // 80-84
    Good,               // 75-79
    Fair,               // 70-74
    Poor                // <70
}
