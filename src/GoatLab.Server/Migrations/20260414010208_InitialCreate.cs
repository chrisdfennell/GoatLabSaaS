using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GoatLab.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CareArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CareArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DosageRate = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DosagePerPound = table.Column<double>(type: "float", nullable: true),
                    Route = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MeatWithdrawalDays = table.Column<int>(type: "int", nullable: true),
                    MilkWithdrawalDays = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Units = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Barns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Barns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Barns_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Checklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Checklists_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsOnWaitingList = table.Column<bool>(type: "bit", nullable: false),
                    WaitingListNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Customers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GrazingAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeoJson = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    Acreage = table.Column<double>(type: "float", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrazingAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrazingAreas_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MapMarkers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MarkerType = table.Column<int>(type: "int", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapMarkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapMarkers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MedicineCabinetItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    MedicationId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<double>(type: "float", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineCabinetItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineCabinetItems_Medications_MedicationId",
                        column: x => x.MedicationId,
                        principalTable: "Medications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicineCabinetItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pastures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    GeoJson = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    Acreage = table.Column<double>(type: "float", nullable: true),
                    PerimeterFeet = table.Column<double>(type: "float", nullable: true),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    StockingCapacity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pastures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pastures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupplierType = table.Column<int>(type: "int", nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Suppliers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TenantMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMembers_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TenantMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccinationProtocols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppliesTo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccinationProtocols", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaccinationProtocols_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Pens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BarnId = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pens_Barns_BarnId",
                        column: x => x.BarnId,
                        principalTable: "Barns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Pens_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ChecklistId = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Checklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "Checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PastureConditionLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PastureId = table.Column<int>(type: "int", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastureConditionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PastureConditionLogs_Pastures_PastureId",
                        column: x => x.PastureId,
                        principalTable: "Pastures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PastureConditionLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PastureRotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PastureId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GoatCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastureRotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PastureRotations_Pastures_PastureId",
                        column: x => x.PastureId,
                        principalTable: "Pastures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PastureRotations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FeedInventory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    FeedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    QuantityOnHand = table.Column<double>(type: "float", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LowStockThreshold = table.Column<double>(type: "float", nullable: true),
                    CostPerUnit = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedInventory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeedInventory_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FeedInventory_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProtocolDoses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    VaccinationProtocolId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    MedicationId = table.Column<int>(type: "int", nullable: true),
                    DayOffset = table.Column<int>(type: "int", nullable: false),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtocolDoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProtocolDoses_Medications_MedicationId",
                        column: x => x.MedicationId,
                        principalTable: "Medications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProtocolDoses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProtocolDoses_VaccinationProtocols_VaccinationProtocolId",
                        column: x => x.VaccinationProtocolId,
                        principalTable: "VaccinationProtocols",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Goats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EarTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Breed = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Registry = table.Column<int>(type: "int", nullable: false),
                    TattooLeft = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TattooRight = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScrapieTag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Microchip = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsExternal = table.Column<bool>(type: "bit", nullable: false),
                    BreederName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SireId = table.Column<int>(type: "int", nullable: true),
                    DamId = table.Column<int>(type: "int", nullable: true),
                    PenId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Goats_Goats_DamId",
                        column: x => x.DamId,
                        principalTable: "Goats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Goats_Goats_SireId",
                        column: x => x.SireId,
                        principalTable: "Goats",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Goats_Pens_PenId",
                        column: x => x.PenId,
                        principalTable: "Pens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Goats_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChecklistCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    ChecklistItemId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistCompletions_ChecklistItems_ChecklistItemId",
                        column: x => x.ChecklistItemId,
                        principalTable: "ChecklistItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecklistCompletions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BodyConditionScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BodyConditionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BodyConditionScores_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BodyConditionScores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BreedingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    DoeId = table.Column<int>(type: "int", nullable: false),
                    BuckId = table.Column<int>(type: "int", nullable: true),
                    BreedingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstimatedDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BreedingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BreedingRecords_Goats_BuckId",
                        column: x => x.BuckId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BreedingRecords_Goats_DoeId",
                        column: x => x.DoeId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BreedingRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CalendarEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AllDay = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    GoatId = table.Column<int>(type: "int", nullable: true),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<int>(type: "int", nullable: false),
                    IsChore = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CalendarEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FamachaScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamachaScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamachaScores_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FamachaScores_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GoatDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoatDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoatDocuments_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoatDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GoatPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoatPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoatPhotos_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoatPhotos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HarvestRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: true),
                    HarvestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Processor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HangingWeight = table.Column<double>(type: "float", nullable: true),
                    PackagedWeight = table.Column<double>(type: "float", nullable: true),
                    ProcessingCost = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    LockerLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HarvestRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HarvestRecords_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_HarvestRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HeatDetections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    DetectedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PredictedNextHeat = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Signs = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeatDetections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeatDetections_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HeatDetections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LinearAppraisals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    AppraisalDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Appraiser = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GeneralAppearance = table.Column<int>(type: "int", nullable: true),
                    DairyCharacter = table.Column<int>(type: "int", nullable: true),
                    BodyCapacity = table.Column<int>(type: "int", nullable: true),
                    MammarySystem = table.Column<int>(type: "int", nullable: true),
                    FinalScore = table.Column<int>(type: "int", nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinearAppraisals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinearAppraisals_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinearAppraisals_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MedicalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MedicationId = table.Column<int>(type: "int", nullable: true),
                    Dosage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AdministeredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Recurrence = table.Column<int>(type: "int", nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalRecords_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicalRecords_Medications_MedicationId",
                        column: x => x.MedicationId,
                        principalTable: "Medications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MedicalRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MilkLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<double>(type: "float", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkLogs_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MilkLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SellerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierId = table.Column<int>(type: "int", nullable: true),
                    GoatId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Purchases_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Purchases_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: true),
                    SaleType = table.Column<int>(type: "int", nullable: false),
                    SaleDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sales_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sales_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Sales_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShowRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    ShowDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ShowName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Class = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Placing = table.Column<int>(type: "int", nullable: true),
                    ClassSize = table.Column<int>(type: "int", nullable: true),
                    Awards = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Judge = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShowRecords_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShowRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GoatId = table.Column<int>(type: "int", nullable: true),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Transactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WeightRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<double>(type: "float", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeightRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeightRecords_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WeightRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "KiddingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    BreedingRecordId = table.Column<int>(type: "int", nullable: false),
                    KiddingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KidsBorn = table.Column<int>(type: "int", nullable: false),
                    KidsAlive = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    DifficultyScore = table.Column<int>(type: "int", nullable: true),
                    AssistanceGiven = table.Column<int>(type: "int", nullable: false),
                    ColostrumGiven = table.Column<bool>(type: "bit", nullable: false),
                    DamStatus = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    KidGoatId = table.Column<int>(type: "int", nullable: true),
                    Complications = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KiddingRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KiddingRecords_BreedingRecords_BreedingRecordId",
                        column: x => x.BreedingRecordId,
                        principalTable: "BreedingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KiddingRecords_Goats_KidGoatId",
                        column: x => x.KidGoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_KiddingRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventCompletions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    CalendarEventId = table.Column<int>(type: "int", nullable: false),
                    OccurrenceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventCompletions_CalendarEvents_CalendarEventId",
                        column: x => x.CalendarEventId,
                        principalTable: "CalendarEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventCompletions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Kids",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    KiddingRecordId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    BirthWeightLbs = table.Column<double>(type: "float", nullable: true),
                    Presentation = table.Column<int>(type: "int", nullable: false),
                    Vigor = table.Column<int>(type: "int", nullable: false),
                    LinkedGoatId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kids_Goats_LinkedGoatId",
                        column: x => x.LinkedGoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Kids_KiddingRecords_KiddingRecordId",
                        column: x => x.KiddingRecordId,
                        principalTable: "KiddingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Kids_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Lactations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    FreshenDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DryOffDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KiddingRecordId = table.Column<int>(type: "int", nullable: true),
                    LactationNumber = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lactations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lactations_Goats_GoatId",
                        column: x => x.GoatId,
                        principalTable: "Goats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Lactations_KiddingRecords_KiddingRecordId",
                        column: x => x.KiddingRecordId,
                        principalTable: "KiddingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Lactations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MilkTestDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    LactationId = table.Column<int>(type: "int", nullable: false),
                    GoatId = table.Column<int>(type: "int", nullable: false),
                    TestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AmLbs = table.Column<double>(type: "float", nullable: true),
                    PmLbs = table.Column<double>(type: "float", nullable: true),
                    TotalLbs = table.Column<double>(type: "float", nullable: false),
                    ButterfatPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    ProteinPercent = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    SomaticCellCount = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MilkTestDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MilkTestDays_Lactations_LactationId",
                        column: x => x.LactationId,
                        principalTable: "Lactations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MilkTestDays_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "CareArticles",
                columns: new[] { "Id", "Category", "Content", "IsBuiltIn", "SortOrder", "Summary", "Title" },
                values: new object[,]
                {
                    { 1, 0, "Starting a goat herd is an exciting venture. Here are the key considerations:\n\n**Breed Selection**\n- **Dairy breeds**: Nubian, Alpine, Saanen, LaMancha — great milk producers\n- **Meat breeds**: Boer, Kiko, Spanish — fast growth and muscling\n- **Fiber breeds**: Angora, Cashmere — for mohair and cashmere fiber\n- **Dual-purpose**: Nigerian Dwarf, Pygmy — milk and companionship\n\n**How Many to Start With**\nGoats are herd animals and should never be kept alone. Start with at least 2-3 goats. A small starter herd of 3-5 does and 1 buck is ideal for most beginners.\n\n**Where to Buy**\n- Reputable breeders with health records\n- Local livestock auctions (inspect carefully)\n- Breed associations and registries\n- Other small farms in your area\n\n**What to Look For**\n- Bright, clear eyes\n- Clean nose (no discharge)\n- Good body condition (not too thin or fat)\n- Sound feet and legs\n- Up-to-date on vaccinations and deworming", true, 1, "How to pick the right breed and number of goats for your farm.", "Choosing Your First Goats" },
                    { 2, 0, "Goats are notorious escape artists. Good fencing is your first investment.\n\n**Fencing Options**\n- **Woven wire (4x4)**: Most popular, 4-foot minimum height\n- **Electric**: Effective training fence, 5+ strands\n- **Cattle panels**: Sturdy and long-lasting, great for small areas\n- **Board fencing**: Attractive but goats may chew wood\n\n**Shelter Requirements**\n- Minimum 15-20 sq ft per goat\n- Three-sided shelter is adequate in mild climates\n- Must be dry and draft-free\n- Good ventilation (ammonia buildup is dangerous)\n- Bedding: straw, wood shavings, or hay\n\n**Key Tips**\n- Goats hate rain more than cold — keep them dry\n- Provide shade in summer\n- Separate bucks from does except during breeding\n- Kidding stalls should be 5x5 ft minimum", true, 2, "Essential fencing types and shelter requirements for goats.", "Fencing & Shelter Basics" },
                    { 3, 0, "A well-planned farm layout saves time and prevents problems.\n\n**Pasture Planning**\n- Allow 250-500 sq ft per goat for exercise areas\n- Rotational grazing: divide pastures into 3-4 paddocks\n- Rest each paddock 4-6 weeks between grazing\n- This breaks parasite cycles and improves forage quality\n\n**Water Systems**\n- Each goat drinks 2-4 gallons per day (more in summer/lactation)\n- Automatic waterers reduce daily chores\n- Keep water clean — goats are picky drinkers\n- Heated buckets or de-icers for winter\n\n**Feed Storage**\n- Secure hay storage (dry, ventilated)\n- Rodent-proof grain bins\n- Mineral feeders accessible to goats but not weather\n- Separate feeding areas to reduce bullying\n\n**Essential Equipment**\n- Hay feeders (keyhole style prevents waste)\n- Grain feeders or troughs\n- Mineral feeders (loose minerals preferred over blocks)\n- Hoof trimming stand or milk stand\n- Basic first aid kit", true, 3, "Infrastructure planning, pasture layout, and water systems.", "Setting Up Your Farm for Goats" },
                    { 4, 0, "Understanding goat behavior helps you manage your herd effectively.\n\n**Herd Hierarchy**\nGoats establish a pecking order. The dominant doe (\"queen\") eats first and gets the best sleeping spot. New additions may be bullied initially — introduce them gradually.\n\n**Body Language**\n- **Tail wagging**: Content, often during feeding\n- **Stamping feet**: Alert/warning to others\n- **Head butting**: Establishing dominance (normal)\n- **Grinding teeth**: Pain or discomfort (investigate!)\n- **Lip curling (flehmen)**: Buck detecting does in heat\n- **Pawing ground**: Boredom or frustration\n\n**Vocalizations**\n- Short bleats: General communication\n- Long, loud calls: Distress, hunger, or separation anxiety\n- Soft murmurs: Doe to kid bonding\n- Sneezing/snorting: Clearing dust or mild irritation\n\n**Common Behaviors**\n- Goats are browsers, not grazers — they prefer brush and weeds over grass\n- They climb everything — secure your structures\n- They're curious and will investigate (and taste) anything new", true, 4, "Herd dynamics, body language, and social structure.", "Understanding Goat Behavior" },
                    { 5, 0, "Before bringing goats home, check your local regulations.\n\n**Zoning**\n- Verify your property is zoned for livestock\n- Some areas allow goats under 'small animal' or 'hobby farm' provisions\n- Urban/suburban areas may have goat-specific ordinances\n- HOAs may have additional restrictions\n\n**Permits & Registration**\n- Many states require a premises ID for livestock\n- Scrapie tags or tattoos may be required for interstate transport\n- Check if you need a livestock permit or farm plan\n- Dairy sales have additional regulations (raw milk laws vary by state)\n\n**Good Neighbor Practices**\n- Keep pens clean to minimize odor\n- Bucks smell strong during rut — plan pen placement accordingly\n- Noise: goats can be vocal, especially at feeding time\n- Maintain setback distances from property lines", true, 5, "Permits, zoning laws, and registration requirements for goat keeping.", "Legal Requirements & Zoning" },
                    { 6, 1, "Prevention is always easier than treatment. Here are the most common issues:\n\n**Parasites (Worms)**\nThe #1 health challenge for goats. Barber pole worm (Haemonchus contortus) causes anemia and can be fatal.\n- Use FAMACHA scoring to monitor anemia\n- Rotate pastures to break parasite cycles\n- Deworm selectively, not on a fixed schedule\n- Fecal egg counts help guide treatment\n\n**Coccidiosis**\nCommon in kids, causes diarrhea and weight loss.\n- Keep bedding dry and clean\n- Preventive coccidiostats in feed for kids\n- Treat with sulfa drugs or amprolium\n\n**Enterotoxemia (Overeating Disease)**\nCaused by Clostridium bacteria when goats overeat grain.\n- Vaccinate with CDT vaccine annually\n- Don't change feed suddenly\n- Limit grain access\n\n**Pneumonia**\nCaused by stress, poor ventilation, or weather changes.\n- Good ventilation in barns (not drafts)\n- Reduce stress during transport/weather changes\n- Vaccinate if endemic in your area\n\n**Foot Rot / Foot Scald**\n- Trim hooves every 6-8 weeks\n- Keep housing dry\n- Treat with zinc sulfate foot baths", true, 1, "Overview of the most common health issues and how to prevent them.", "Common Goat Diseases & Prevention" },
                    { 7, 1, "**Core Vaccines**\n\n**CDT (Clostridium Perfringens Types C&D + Tetanus)**\nThis is the single most important vaccine for goats.\n- Kids: First dose at 4-6 weeks, booster 3-4 weeks later\n- Adults: Annual booster\n- Pregnant does: Booster 4-6 weeks before kidding (passes immunity to kids through colostrum)\n\n**Optional Vaccines (based on your area)**\n- CLA (Caseous Lymphadenitis): If prevalent in your herd/area\n- Rabies: Required in some areas, especially if wildlife contact possible\n- Chlamydia: If abortion storms are a concern\n- Pneumonia vaccines: In herds with chronic respiratory issues\n\n**Vaccination Tips**\n- Store vaccines properly (refrigerate, don't freeze)\n- Use clean needles (18-20 gauge, 3/4 to 1 inch)\n- SubQ (under the skin) injections in the neck or behind the shoulder\n- Record everything — date, vaccine, lot number, goat ID\n- Watch for rare allergic reactions for 30 minutes after injection", true, 2, "Core and optional vaccinations for goats by age.", "Vaccination Schedule" },
                    { 8, 1, "FAMACHA is a simple field test to detect anemia caused by the barber pole worm.\n\n**How to Score**\n1. Restrain the goat gently\n2. Pull down the lower eyelid\n3. Compare the inner eyelid color to the FAMACHA card\n4. Score 1-5:\n\n**Score 1 — Red**: Optimal. Healthy, not anemic.\n**Score 2 — Red-Pink**: Acceptable. Monitor.\n**Score 3 — Pink**: Borderline. Consider deworming.\n**Score 4 — Pink-White**: Anemic. Deworm immediately.\n**Score 5 — White**: Severely anemic. Deworm and consider supportive care (iron, B12). May need veterinary attention.\n\n**When to Check**\n- Every 2-4 weeks during warm/wet months (peak parasite season)\n- Monthly during cool/dry months\n- Any time a goat looks lethargic or has a rough coat\n- Before and after deworming to assess effectiveness\n\n**Best Practices**\n- Check in natural light for accurate color assessment\n- Score consistently — same person if possible\n- Record scores to track trends per goat\n- Combine with fecal egg counts for full picture\n- Only deworm goats scoring 3 or higher (selective deworming)", true, 3, "How to check eyelid color to assess anemia from barber pole worm.", "FAMACHA Scoring Guide" },
                    { 9, 1, "Body Condition Scoring (BCS) rates a goat's fat reserves on a 1-5 scale by feel.\n\n**How to Score**\nFeel the goat over the ribs, spine, and loin area behind the last rib.\n\n**Score 1 — Emaciated**: Spine and ribs sharp, no fat cover. Requires immediate nutritional intervention.\n**Score 2 — Thin**: Ribs easily felt, spine prominent. Increase feed quality/quantity.\n**Score 3 — Ideal**: Ribs felt with slight pressure, smooth spine. Maintain current feeding.\n**Score 4 — Fat**: Ribs hard to feel, spine rounded over. Reduce grain, increase exercise.\n**Score 5 — Obese**: Cannot feel ribs, thick fat deposits. Significant diet change needed.\n\n**Ideal BCS by Stage**\n- Dry does: 3.0-3.5\n- Late pregnancy: 3.0-3.5 (don't let them get too fat — pregnancy toxemia risk)\n- Early lactation: 2.5-3.0 (some weight loss normal)\n- Bucks in rut: May drop to 2.0-2.5 (normal, recover after)\n- Growing kids: 3.0\n\n**Tips**\n- Score monthly and record in GoatLab\n- Hair coat can hide condition — always feel, don't just look\n- Sudden changes indicate health issues", true, 4, "Assess your goat's body fat and health by feel.", "Body Condition Scoring" },
                    { 10, 1, "Regular hoof care prevents lameness, foot rot, and pain.\n\n**When to Trim**\n- Every 6-8 weeks (more often on soft ground, less on rocky terrain)\n- Any time hooves look overgrown or curled\n- Before shows or sales\n\n**Tools Needed**\n- Sharp hoof shears or trimmers\n- Hoof knife (for detailed work)\n- Blood stop powder (in case of over-trimming)\n- Gloves\n- Milk stand or trimming stand\n\n**Step-by-Step**\n1. Secure the goat on a stand or have a helper hold\n2. Clean out dirt and debris with the tip of the shears\n3. Trim the overgrown wall — cut parallel to the growth rings\n4. Trim the heel to be even with the sole\n5. Flatten the sole — it should be flat, not cupped\n6. The goal: the bottom of the hoof looks like a flat, pink triangle\n7. If you see pink, stop — you're close to the quick\n8. If you draw blood, apply blood stop powder and don't panic\n\n**Hoof Health**\n- Keep bedding dry (wet = foot rot)\n- Zinc sulfate foot baths for prevention\n- Treat foot rot immediately (trim + topical treatment)\n- Biotin supplements can improve hoof quality", true, 5, "Step-by-step hoof care and trimming schedule.", "Hoof Trimming Guide" },
                    { 11, 1, "Be prepared for emergencies. Stock these supplies:\n\n**Must-Have Supplies**\n- Digital rectal thermometer (normal goat temp: 101.5-103.5°F)\n- Syringes (3cc, 6cc, 12cc) and needles (18-20 gauge)\n- CDT vaccine\n- Dewormer (at least 2 different classes)\n- Electrolyte powder or Gatorade\n- Pepto-Bismol or kaolin-pectin (for scours)\n- Iodine or Betadine (for wound/navel care)\n- Blood stop powder\n- Activated charcoal (for poisoning)\n- Baking soda (for bloat)\n- Vegetable/mineral oil (for bloat)\n- Probiotics paste\n- Nutridrench or CMPK (for pregnancy toxemia/milk fever)\n- Banamine (prescription — ask your vet)\n- Penicillin or LA-200 (prescription)\n\n**Kidding Kit Additions**\n- OB lube\n- OB gloves (shoulder length)\n- Bulb syringe (to clear kid's airway)\n- Dental floss or umbilical clamps (for cords)\n- Iodine (7% for navel dipping)\n- Towels\n- Heat lamp or hair dryer\n- Colostrum replacer (frozen colostrum is better)\n- Bottle and Pritchard nipple\n\n**When to Call the Vet**\n- Temperature over 104°F or under 100°F\n- Labored breathing\n- Inability to stand\n- Bloat not responding to treatment\n- Kidding difficulties (no progress after 30 min of active labor)\n- Severe bleeding or injury", true, 6, "Essential supplies every goat owner should have on hand.", "Emergency First Aid Kit" },
                    { 12, 2, "**Breeding Season**\nMost goat breeds are seasonal breeders (fall/winter). Some breeds like Nigerian Dwarf can breed year-round.\n- Typical season: August through February\n- Triggered by decreasing daylight hours\n- Bucks become more odorous and aggressive during rut\n\n**Age & Maturity**\n- Does: Breed at 7-10 months or when they reach 60-70% of adult weight\n- Bucks: Fertile by 4-5 months (separate early!)\n- Don't breed too young — stunts growth and increases complications\n\n**Buck-to-Doe Ratios**\n- 1 buck per 25-30 does (mature buck)\n- 1 young buck per 10-15 does\n- Keep backup bucks when possible\n\n**Heat Cycle**\n- Cycle length: 18-24 days (average 21 days)\n- Standing heat lasts: 12-36 hours\n- Signs: tail wagging, vocalization, swollen/red vulva, mounting other does, decreased appetite\n- Breed during standing heat for best results\n\n**Methods**\n- Pen breeding: Put buck with doe(s) for 2-3 heat cycles\n- Hand breeding: Supervised single mating, record exact date\n- AI (Artificial Insemination): Requires training and equipment", true, 1, "When and how to breed goats, buck-to-doe ratios, and breeding season.", "Breeding Basics" },
                    { 13, 2, "Goat gestation averages **150 days** (145-155 day range).\n\n**Pregnancy Timeline**\n- **Day 1-30**: Embryo implantation. Avoid stress and handling.\n- **Day 30-90**: Fetal development. Maintain normal diet.\n- **Day 90-120**: Rapid fetal growth. Gradually increase nutrition.\n- **Day 120-150**: Final growth. Increase grain, supplement selenium/vitamin E.\n\n**Nutrition**\n- First 3 months: Good hay and minerals are sufficient\n- Last 6 weeks: Increase grain gradually (up to 1 lb/day)\n- Provide selenium/vitamin E supplement (if deficient in your area)\n- Free-choice loose minerals always available\n- Fresh clean water — pregnant does drink more\n\n**CDT Vaccination**\nBooster 4-6 weeks before due date. This passes immunity to kids through colostrum.\n\n**Warning Signs**\n- Vaginal discharge (clear mucus near term is normal; colored discharge is not)\n- Loss of appetite for more than 24 hours\n- Grinding teeth (pain)\n- Swelling in legs/udder is normal near term\n- Lying down and not getting up — check for pregnancy toxemia\n\n**Pregnancy Toxemia Prevention**\n- Don't let does get too fat OR too thin\n- Ensure adequate nutrition in last 6 weeks\n- Reduce stress (don't transport, change housing, etc.)\n- Does carrying multiples are at higher risk", true, 2, "What to expect during the 150-day gestation period.", "Gestation & Pregnancy Care" },
                    { 14, 2, "**Pre-Labor Signs (Days Before)**\n- Ligaments around tail head soften/disappear\n- Udder fills and becomes tight\n- Vulva swells and elongates\n- Doe becomes restless, paws at ground\n- May separate from herd\n\n**Active Labor**\n- **Stage 1** (2-12 hours): Contractions begin, doe is restless, may talk to her belly\n- **Stage 2** (30 min - 2 hours): Active pushing, water bag appears, kid delivered\n- **Stage 3** (up to 12 hours): Placenta passed\n\n**Normal Presentations**\n- Front feet first with nose resting on legs (diving position) — ideal\n- Both front feet with head — normal\n- Rear feet first (breech) — assist gently\n\n**When to Intervene**\n- Hard pushing for 30+ minutes with no progress\n- Only one foot visible (other leg may be back)\n- Head visible but no feet\n- Kid stuck at shoulders or hips\n- Doe exhausted and stopped pushing\n\n**Newborn Kid Care**\n1. Clear airway — remove mucus from nose and mouth\n2. Stimulate breathing — rub vigorously with towel\n3. Dip navel in 7% iodine\n4. Ensure kid nurses within 1-2 hours (colostrum is critical)\n5. Watch for hypothermia — dry kid and provide warmth if needed", true, 3, "Signs of labor, normal delivery, and when to intervene.", "Kidding: What to Expect" },
                    { 15, 2, "**Dam-Raised vs Bottle-Fed**\n- **Dam-raised**: Less work, natural bonding, doe handles feeding. Best for meat breeds.\n- **Bottle-fed**: Friendlier kids, control over milk amount, necessary if doe rejects kid or has mastitis.\n\n**Bottle Feeding Schedule**\n- Day 1-3: Colostrum only, 2-4 oz every 2-4 hours\n- Week 1-2: 4-6 oz, 4 times daily\n- Week 3-4: 8-12 oz, 3 times daily\n- Week 5-8: 12-16 oz, 2 times daily\n- Wean at 8-12 weeks\n\n**Key Milestones**\n- Start offering hay and a little grain at 1-2 weeks\n- Disbud (if desired) at 3-10 days (breed dependent)\n- CDT vaccine at 4-6 weeks, booster at 8-10 weeks\n- Coccidia prevention starting at 3-4 weeks\n- Wean when eating well and at least 2-2.5x birth weight\n\n**Common Kid Issues**\n- Floppy Kid Syndrome: Weak, can't stand. Often responds to baking soda and electrolytes.\n- Hypothermia: Warm gradually, feed warm colostrum\n- Scours (diarrhea): Electrolytes, reduce milk, treat cause\n- Naval ill: Prevent by dipping navel in iodine at birth", true, 4, "Dam-raised vs bottle-fed, weaning, and kid health.", "Raising Kids (Baby Goats)" },
                    { 16, 3, "Proper nutrition is the foundation of a healthy herd.\n\n**Hay (Foundation of the Diet)**\n- 2-4 lbs per goat per day (about 3-5% of body weight)\n- Grass hay: Good maintenance diet\n- Alfalfa: Higher protein, great for lactating does and growing kids\n- Mixed grass/alfalfa: Good all-purpose option\n- Always provide free-choice hay\n\n**Grain (Supplemental)**\n- Lactating does: 1-2 lbs/day depending on production\n- Late pregnancy: 0.5-1 lb/day\n- Bucks in rut: 0.5-1 lb/day\n- Maintenance (dry does, wethers): Little to no grain needed\n- Introduce and change grain gradually over 7-10 days\n\n**Minerals**\n- Loose goat-specific minerals (NOT sheep minerals — goats need copper)\n- Free-choice, always available\n- Baking soda free-choice (helps with rumen pH)\n\n**Water**\n- 2-4 gallons per goat per day\n- More in summer and during lactation\n- Clean and fresh — goats are picky\n\n**Treats (in moderation)**\n- Fruit, vegetables, bread, animal crackers\n- Avoid: chocolate, avocado, wild cherry, rhododendron, azalea", true, 1, "What to feed, how much, and feeding schedules.", "Daily Feeding Guide" },
                    { 17, 3, "**Getting Started**\n- Does need to freshen (give birth) before they produce milk\n- First 3-5 days: Colostrum for kids only\n- Begin milking once kids are old enough to share or are weaned\n\n**Equipment**\n- Milk stand with head catch\n- Stainless steel or food-grade bucket\n- Teat dip or udder wash\n- Milk strainer and filters\n- Glass jars for storage\n\n**Milking Procedure**\n1. Secure doe on milk stand with grain\n2. Wash udder with warm water or udder wash\n3. Strip first few squirts into a strip cup (check for clots/blood)\n4. Milk with full-hand squeeze: trap milk with thumb and forefinger, squeeze down with remaining fingers\n5. Alternate hands rhythmically\n6. Milk until udder feels soft and flat\n7. Apply teat dip after milking\n8. Strain milk immediately and chill to 40°F within an hour\n\n**Milk Production**\n- Nigerian Dwarf: 1-3 lbs/day\n- Standard dairy breeds: 6-12 lbs/day\n- Peak production: 4-8 weeks after kidding\n- Lactation length: 10-12 months\n\n**Schedule**\n- Milk every 12 hours for best production\n- Consistency is key — same time each day\n- Once daily milking is possible with reduced yield", true, 2, "How to hand milk, equipment, and milk handling.", "Milking Basics" },
                    { 18, 3, "**Spring**\n- Deworm based on FAMACHA scores (parasite season begins)\n- Begin rotational grazing\n- CDT boosters for pregnant does (4-6 weeks before kidding)\n- Prepare kidding stalls\n- Start coccidia prevention for kids\n- Hoof trimming\n- Check/repair fencing after winter\n\n**Summer**\n- Provide shade and ample water\n- Monitor for heat stress (panting, lethargy)\n- FAMACHA checks every 2-3 weeks\n- Fly control (fly traps, sprays)\n- Maintain pasture rotation\n- Trim hooves\n\n**Fall**\n- Breeding season begins\n- Put bucks with does (record dates!)\n- Annual CDT vaccination\n- Increase nutrition for bred does\n- Stock up on hay for winter\n- Trim hooves\n- Prepare shelters for winter\n\n**Winter**\n- Ensure unfrozen water (heated buckets)\n- Increase hay for cold weather calories\n- Check shelter for drafts (ventilation without drafts)\n- Kidding season (if fall-bred)\n- Monitor body condition — increase feed if needed\n- Reduce hoof trimming frequency\n- Plan next year's breeding", true, 3, "Month-by-month and seasonal task guides.", "Seasonal Farm Checklists" },
                    { 19, 3, "Good pasture management reduces parasites and improves herd health.\n\n**Rotational Grazing Basics**\n- Divide pasture into 3-4+ paddocks\n- Graze each paddock for 5-7 days\n- Rest each paddock for 4-6 weeks minimum\n- Move animals when forage is grazed to 3-4 inches\n- Never overgraze — it weakens plants and increases parasites\n\n**Parasite Control Through Grazing**\n- Most larvae are in the bottom 2 inches of forage\n- Don't graze below 4 inches\n- Sun and heat kill larvae — rest during summer is more effective\n- Multi-species grazing (cattle/horses with goats) breaks parasite cycles\n- Mow after grazing to expose larvae to sunlight\n\n**Stocking Rates**\n- General guideline: 6-8 goats per acre of good pasture\n- Depends on forage quality, rainfall, and supplemental feeding\n- Overstocking = more parasites, less forage, poor condition\n\n**Improving Pastures**\n- Soil test every 2-3 years\n- Lime and fertilize based on test results\n- Overseed thin areas\n- Goats prefer browse (shrubs, weeds, brush) over grass\n- Planting browse species (willows, mulberry) can supplement grazing\n\n**Pasture Condition Scoring**\nRate pastures 1-5 based on forage density, diversity, weed pressure, and ground cover. Track in GoatLab to optimize rotation timing.", true, 4, "Rotational grazing, parasite control, and forage management.", "Pasture Management" },
                    { 20, 4, "Tracking milk production helps you make breeding, feeding, and culling decisions.\n\n**What to Track**\n- Daily yield per goat (weigh milk in lbs or measure in cups)\n- AM vs PM if milking twice daily\n- Milk quality notes (off taste, color changes, clots)\n- Days in milk (DIM) — how many days since kidding\n\n**Why Track**\n- Identify top producers for breeding\n- Detect mastitis early (sudden drop in production)\n- Optimize feeding (high producers need more grain)\n- Plan dry-off timing (when to stop milking before next kidding)\n- Calculate cost-per-gallon and profitability\n\n**Typical Lactation Curve**\n- Weeks 1-2: Production ramps up\n- Weeks 4-8: Peak production\n- Months 3-10: Gradual decline\n- Month 10-12: Dry off (stop milking 2 months before next kidding)\n\n**Using GoatLab**\nLog milk daily from the Dashboard quick-entry or the Milk Production page. View trend charts to spot patterns and compare does.", true, 1, "Why and how to track daily milk yield per goat.", "Tracking Milk Production" },
                    { 21, 4, "**Selling Live Animals**\n- Build a reputation through quality animals and honest descriptions\n- Take good photos (side profile, udder for does, muscling for bucks)\n- Provide health records and registration papers\n- Price based on breed, quality, age, and your market\n- Use GoatLab's sales tracking and customer CRM features\n\n**Selling Milk & Dairy Products**\n- Know your state's raw milk laws before selling\n- Grade A dairy requires licensed facilities in most states\n- Goat milk soap is often legal without dairy licensing\n- Cheese, yogurt, etc. typically require food processing licenses\n\n**Selling Meat**\n- USDA inspection required for retail sales in most cases\n- Custom slaughter (buyer purchases live animal, pays for processing) avoids some regulations\n- Halal and ethnic markets can be strong demand\n- Track hanging weight and packaged weight for pricing\n\n**Marketing Channels**\n- Farm website and social media\n- Local farmers markets\n- Craigslist / Facebook Marketplace\n- Breed association classified ads\n- Word of mouth — your best customers refer others", true, 2, "Tips for marketing and selling goats, milk, and meat.", "Selling Goats & Products" },
                    { 22, 4, "Good records help you make smart farm decisions and simplify taxes.\n\n**What to Track**\n- All income: animal sales, milk sales, breeding fees\n- All expenses: feed, hay, vet bills, supplies, equipment\n- Assign costs to specific goats when possible (for cost analysis)\n\n**Key Metrics**\n- **Cost per goat per month**: Total expenses / number of goats / months\n- **Cost per gallon of milk**: (Feed + supplies + labor) / gallons produced\n- **Break-even price**: Total annual costs / number of animals sold\n- **Return per doe**: Income from doe (milk + kids) minus her costs\n\n**Tax Considerations**\n- Farm income/loss reported on Schedule F\n- Keep receipts for all farm purchases\n- Mileage to/from farm supply stores, vet, etc.\n- Equipment depreciation\n- Consult a tax professional familiar with agricultural exemptions\n\n**Using GoatLab**\nLog every transaction in the Finance section. Use the cost-per-goat analysis to identify which animals are profitable and which are costing you money. Export CSV reports for your accountant.", true, 3, "Track income, expenses, and calculate cost-per-goat.", "Financial Record Keeping" },
                    { 23, 5, "**Dairy Breeds**\n- **Nubian**: Roman nose, long ears, rich milk (high butterfat). Loud.\n- **Alpine**: Hardy, high producers, many color patterns.\n- **Saanen**: White, highest volume producers, gentle.\n- **LaMancha**: Very short ears, friendly, good milk quality.\n- **Nigerian Dwarf**: Small (under 75 lbs), highest butterfat, breed year-round.\n- **Oberhasli**: Bay colored, moderate production, quiet.\n- **Toggenburg**: Swiss breed, oldest registered dairy breed, moderate production.\n\n**Meat Breeds**\n- **Boer**: Large, white body/red head, fast growth, excellent muscling.\n- **Kiko**: Hardy, parasite resistant, good mothers, NZ origin.\n- **Spanish**: Hardy range goats, lean meat, excellent foragers.\n- **Savanna**: White, heat tolerant, good mothers, from South Africa.\n- **Myotonic (Fainting)**: Muscle condition causes stiffness, heavily muscled.\n\n**Fiber Breeds**\n- **Angora**: Produces mohair, require shearing twice yearly.\n- **Cashmere**: Fine undercoat harvested annually, any breed can produce.\n\n**Miniature Breeds**\n- **Pygmy**: Stocky, compact, primarily pets/companions. 60-80 lbs.\n- **Mini breeds**: Crosses of Nigerian Dwarf with standard dairy breeds.", true, 1, "Quick reference for popular dairy, meat, and fiber breeds.", "Common Goat Breeds Reference" },
                    { 24, 5, "**Vital Signs**\n- Temperature: 101.5-103.5°F (rectal)\n- Heart rate: 70-90 beats/min (adult), 100-120 (kids)\n- Respiration: 12-25 breaths/min\n- Rumen contractions: 1-2 per minute\n\n**Reproduction**\n- Heat cycle: 18-24 days (avg 21)\n- Gestation: 145-155 days (avg 150)\n- Breeding age: 7-10 months (60-70% adult weight)\n- Kids per birth: 1-4 (twins most common)\n\n**Weight Ranges (Adult)**\n- Nigerian Dwarf: 50-75 lbs\n- Pygmy: 60-80 lbs\n- Alpine/Saanen/Nubian: 130-200 lbs\n- Boer: 200-340 lbs\n\n**Milk Production (Daily Average)**\n- Nigerian Dwarf: 1-3 lbs\n- Nubian: 4-8 lbs\n- Alpine/Saanen: 6-12 lbs\n- LaMancha: 5-9 lbs\n\n**Feed Requirements**\n- Hay: 3-5% of body weight daily\n- Water: 2-4 gallons daily (more in heat/lactation)\n- Grain: 0-2 lbs/day depending on stage\n\n**Lifespan**: 10-15 years (does), 8-12 years (bucks)", true, 2, "Quick-reference vital signs, weights, and production numbers.", "Normal Vital Signs & Reference Numbers" },
                    { 25, 5, "Goats are browsers and will sample many plants. Most are fine, but some are toxic.\n\n**Highly Toxic (Can Be Fatal)**\n- Azalea / Rhododendron\n- Yew (all parts)\n- Oleander\n- Water hemlock\n- Poison hemlock\n- Cherry (wilted leaves — fresh and dry are OK)\n- Mountain laurel\n- Lily of the valley\n\n**Moderately Toxic (Illness)**\n- Rhubarb leaves\n- Raw potatoes (green parts)\n- Nightshade family\n- Bracken fern\n- Jimsonweed\n- Milkweed\n- Oak (excess acorns)\n\n**Generally Safe Plants Goats Love**\n- Multiflora rose\n- Honeysuckle\n- Blackberry/raspberry brambles\n- Kudzu\n- Clover\n- Chicory\n- Plantain (the weed, not the banana)\n- Willow\n- Mulberry\n\n**Prevention**\n- Walk your pastures and identify plants before introducing goats\n- Remove or fence off toxic plants\n- Well-fed goats are less likely to eat toxic plants\n- Provide free-choice baking soda (helps with mild toxin ingestion)", true, 3, "Plants to keep away from your goats.", "Poisonous Plants for Goats" },
                    { 26, 5, "**Animal Terms**\n- **Doe / Nanny**: Adult female goat\n- **Buck / Billy**: Adult male goat (intact)\n- **Wether**: Castrated male goat\n- **Doeling**: Young female (under 1 year)\n- **Buckling**: Young male (under 1 year)\n- **Kid**: Baby goat of either gender\n- **Yearling**: Goat between 1-2 years old\n\n**Breeding Terms**\n- **Freshen**: To give birth and begin producing milk\n- **Dry**: Not currently producing milk\n- **In kid**: Pregnant\n- **Kidding**: Giving birth\n- **Dam**: Mother\n- **Sire**: Father\n- **Rut**: Buck breeding season (increased hormones, odor)\n- **Standing heat**: When doe is receptive to breeding\n\n**Health Terms**\n- **FAMACHA**: Eyelid color chart for anemia detection\n- **BCS**: Body Condition Score (1-5 fat assessment)\n- **CDT**: Core vaccine (Clostridium + Tetanus)\n- **Scours**: Diarrhea\n- **Bloat**: Rumen gas buildup (emergency)\n- **Ketosis/Pregnancy toxemia**: Metabolic disease in late pregnancy\n- **CAE**: Caprine Arthritis Encephalitis (viral disease)\n- **CL**: Caseous Lymphadenitis (abscesses)\n- **Mastitis**: Udder infection\n\n**Production Terms**\n- **Butterfat**: Fat content of milk (4-10% in goats)\n- **DIM**: Days in Milk (since last kidding)\n- **Dry off**: Stopping milking to rest the doe before next kidding\n- **Colostrum**: First milk after birth, rich in antibodies", true, 4, "Common terminology used in goat farming.", "Glossary of Goat Terms" }
                });

            migrationBuilder.InsertData(
                table: "Medications",
                columns: new[] { "Id", "Description", "DosagePerPound", "DosageRate", "MeatWithdrawalDays", "MilkWithdrawalDays", "Name", "Notes", "Route" },
                values: new object[,]
                {
                    { 1, "Clostridium Perfringens Types C&D + Tetanus. Core vaccine for all goats.", null, "2 mL per goat regardless of size", null, null, "CDT Vaccine", "Annual booster. Pregnant does: 4-6 weeks before kidding. Kids: 4-6 weeks then booster at 8-10 weeks.", "SubQ" },
                    { 2, "Broad-spectrum dewormer effective against roundworms, lungworms, and external parasites.", 0.02, "1 mL per 50 lbs (oral, cattle formulation)", 35, 9, "Ivermectin (Ivomec)", "Give orally for goats — higher bioavailability than injection.", "Oral or SubQ" },
                    { 3, "Dewormer effective against roundworms, some tapeworms.", 0.20000000000000001, "1 mL per 5 lbs (10% liquid suspension)", 16, 4, "Fenbendazole (SafeGuard)", "Double or triple the cattle dose for goats. 3-day treatment for Meningeal worm.", "Oral" },
                    { 4, "Broad-spectrum dewormer including liver flukes and tapeworms.", 0.040000000000000001, "1 mL per 25 lbs", 27, 7, "Albendazole (Valbazen)", "Do NOT use in first 30 days of pregnancy — can cause birth defects.", "Oral" },
                    { 5, "Antibiotic for respiratory infections, foot rot, wound infections.", 0.050000000000000003, "1 mL per 20 lbs twice daily for 5 days", 30, 4, "Penicillin G Procaine", "Refrigerate. Give for full course — don't stop early.", "SubQ or IM" },
                    { 6, "Long-acting antibiotic for respiratory and systemic infections.", 0.050000000000000003, "1 mL per 20 lbs every 48-72 hours", 28, 7, "LA-200 (Oxytetracycline)", "Can cause tissue irritation at injection site. Rotate sites.", "SubQ or IM" },
                    { 7, "NSAID pain reliever and anti-inflammatory. Prescription required.", 0.01, "1 mL per 100 lbs", 30, 4, "Banamine (Flunixin)", "Never give IM — causes tissue necrosis. IV preferred, oral paste also effective.", "IV or Oral" },
                    { 8, "Treatment and prevention of coccidiosis, especially in kids.", 0.40000000000000002, "Treatment: 10 mL per 25 lbs of 9.6% solution for 5 days", 0, 0, "Corid (Amprolium)", "Prevention dose is half the treatment dose. Treat for full 5 days.", "Oral (drench or in water)" },
                    { 9, "Energy supplement for weak, ketotic, or post-kidding does and kids.", null, "Adults: 2 oz. Kids: 1/2 oz", null, null, "Nutridrench", "Keep on hand for emergencies. Provides quick energy, vitamins, and minerals.", "Oral" },
                    { 10, "Probiotic paste to restore rumen bacteria after illness, stress, or antibiotic use.", null, "5 grams per goat", null, null, "Probios (Probiotics)", "Give after antibiotic treatment, during diet changes, or after stressful events.", "Oral" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Barns_TenantId",
                table: "Barns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BodyConditionScores_GoatId",
                table: "BodyConditionScores",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_BodyConditionScores_TenantId",
                table: "BodyConditionScores",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingRecords_BuckId",
                table: "BreedingRecords",
                column: "BuckId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingRecords_DoeId",
                table: "BreedingRecords",
                column: "DoeId");

            migrationBuilder.CreateIndex(
                name: "IX_BreedingRecords_TenantId",
                table: "BreedingRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_GoatId",
                table: "CalendarEvents",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_Start",
                table: "CalendarEvents",
                column: "Start");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_TenantId",
                table: "CalendarEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistCompletions_ChecklistItemId",
                table: "ChecklistCompletions",
                column: "ChecklistItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistCompletions_Date",
                table: "ChecklistCompletions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistCompletions_TenantId",
                table: "ChecklistCompletions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_ChecklistId",
                table: "ChecklistItems",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_TenantId",
                table: "ChecklistItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_TenantId",
                table: "Checklists",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId",
                table: "Customers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EventCompletions_CalendarEventId_OccurrenceDate",
                table: "EventCompletions",
                columns: new[] { "CalendarEventId", "OccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventCompletions_TenantId",
                table: "EventCompletions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FamachaScores_GoatId",
                table: "FamachaScores",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_FamachaScores_TenantId",
                table: "FamachaScores",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedInventory_SupplierId",
                table: "FeedInventory",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedInventory_TenantId",
                table: "FeedInventory",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoatDocuments_GoatId",
                table: "GoatDocuments",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_GoatDocuments_TenantId",
                table: "GoatDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GoatPhotos_GoatId",
                table: "GoatPhotos",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_GoatPhotos_TenantId",
                table: "GoatPhotos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_DamId",
                table: "Goats",
                column: "DamId");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_Name",
                table: "Goats",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_PenId",
                table: "Goats",
                column: "PenId");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_SireId",
                table: "Goats",
                column: "SireId");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_Status",
                table: "Goats",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Goats_TenantId",
                table: "Goats",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GrazingAreas_TenantId",
                table: "GrazingAreas",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestRecords_GoatId",
                table: "HarvestRecords",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_HarvestRecords_TenantId",
                table: "HarvestRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HeatDetections_GoatId",
                table: "HeatDetections",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_HeatDetections_TenantId",
                table: "HeatDetections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_BreedingRecordId",
                table: "KiddingRecords",
                column: "BreedingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_KidGoatId",
                table: "KiddingRecords",
                column: "KidGoatId");

            migrationBuilder.CreateIndex(
                name: "IX_KiddingRecords_TenantId",
                table: "KiddingRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Kids_KiddingRecordId",
                table: "Kids",
                column: "KiddingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Kids_LinkedGoatId",
                table: "Kids",
                column: "LinkedGoatId");

            migrationBuilder.CreateIndex(
                name: "IX_Kids_TenantId",
                table: "Kids",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_GoatId_FreshenDate",
                table: "Lactations",
                columns: new[] { "GoatId", "FreshenDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_KiddingRecordId",
                table: "Lactations",
                column: "KiddingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Lactations_TenantId",
                table: "Lactations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LinearAppraisals_GoatId",
                table: "LinearAppraisals",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_LinearAppraisals_TenantId",
                table: "LinearAppraisals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MapMarkers_TenantId",
                table: "MapMarkers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_GoatId",
                table: "MedicalRecords",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_MedicationId",
                table: "MedicalRecords",
                column: "MedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_NextDueDate",
                table: "MedicalRecords",
                column: "NextDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_TenantId",
                table: "MedicalRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineCabinetItems_MedicationId",
                table: "MedicineCabinetItems",
                column: "MedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineCabinetItems_TenantId",
                table: "MedicineCabinetItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkLogs_Date",
                table: "MilkLogs",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_MilkLogs_GoatId",
                table: "MilkLogs",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkLogs_TenantId",
                table: "MilkLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkTestDays_LactationId",
                table: "MilkTestDays",
                column: "LactationId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkTestDays_TenantId",
                table: "MilkTestDays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MilkTestDays_TestDate",
                table: "MilkTestDays",
                column: "TestDate");

            migrationBuilder.CreateIndex(
                name: "IX_PastureConditionLogs_PastureId",
                table: "PastureConditionLogs",
                column: "PastureId");

            migrationBuilder.CreateIndex(
                name: "IX_PastureConditionLogs_TenantId",
                table: "PastureConditionLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PastureRotations_PastureId",
                table: "PastureRotations",
                column: "PastureId");

            migrationBuilder.CreateIndex(
                name: "IX_PastureRotations_TenantId",
                table: "PastureRotations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Pastures_TenantId",
                table: "Pastures",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Pens_BarnId",
                table: "Pens",
                column: "BarnId");

            migrationBuilder.CreateIndex(
                name: "IX_Pens_TenantId",
                table: "Pens",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolDoses_MedicationId",
                table: "ProtocolDoses",
                column: "MedicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolDoses_TenantId",
                table: "ProtocolDoses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProtocolDoses_VaccinationProtocolId",
                table: "ProtocolDoses",
                column: "VaccinationProtocolId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_GoatId",
                table: "Purchases",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_SupplierId",
                table: "Purchases",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TenantId",
                table: "Purchases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_CustomerId",
                table: "Sales",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_GoatId",
                table: "Sales",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_TenantId",
                table: "Sales",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowRecords_GoatId",
                table: "ShowRecords",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_ShowRecords_TenantId",
                table: "ShowRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId",
                table: "Suppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_ApplicationUserId",
                table: "TenantMembers",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMembers_TenantId_UserId",
                table: "TenantMembers",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_GoatId",
                table: "Transactions",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId",
                table: "Transactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_VaccinationProtocols_TenantId",
                table: "VaccinationProtocols",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_GoatId",
                table: "WeightRecords",
                column: "GoatId");

            migrationBuilder.CreateIndex(
                name: "IX_WeightRecords_TenantId",
                table: "WeightRecords",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BodyConditionScores");

            migrationBuilder.DropTable(
                name: "CareArticles");

            migrationBuilder.DropTable(
                name: "ChecklistCompletions");

            migrationBuilder.DropTable(
                name: "EventCompletions");

            migrationBuilder.DropTable(
                name: "FamachaScores");

            migrationBuilder.DropTable(
                name: "FeedInventory");

            migrationBuilder.DropTable(
                name: "GoatDocuments");

            migrationBuilder.DropTable(
                name: "GoatPhotos");

            migrationBuilder.DropTable(
                name: "GrazingAreas");

            migrationBuilder.DropTable(
                name: "HarvestRecords");

            migrationBuilder.DropTable(
                name: "HeatDetections");

            migrationBuilder.DropTable(
                name: "Kids");

            migrationBuilder.DropTable(
                name: "LinearAppraisals");

            migrationBuilder.DropTable(
                name: "MapMarkers");

            migrationBuilder.DropTable(
                name: "MedicalRecords");

            migrationBuilder.DropTable(
                name: "MedicineCabinetItems");

            migrationBuilder.DropTable(
                name: "MilkLogs");

            migrationBuilder.DropTable(
                name: "MilkTestDays");

            migrationBuilder.DropTable(
                name: "PastureConditionLogs");

            migrationBuilder.DropTable(
                name: "PastureRotations");

            migrationBuilder.DropTable(
                name: "ProtocolDoses");

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropTable(
                name: "Sales");

            migrationBuilder.DropTable(
                name: "ShowRecords");

            migrationBuilder.DropTable(
                name: "TenantMembers");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "WeightRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "CalendarEvents");

            migrationBuilder.DropTable(
                name: "Lactations");

            migrationBuilder.DropTable(
                name: "Pastures");

            migrationBuilder.DropTable(
                name: "Medications");

            migrationBuilder.DropTable(
                name: "VaccinationProtocols");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Checklists");

            migrationBuilder.DropTable(
                name: "KiddingRecords");

            migrationBuilder.DropTable(
                name: "BreedingRecords");

            migrationBuilder.DropTable(
                name: "Goats");

            migrationBuilder.DropTable(
                name: "Pens");

            migrationBuilder.DropTable(
                name: "Barns");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
