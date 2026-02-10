// =============================================================================
// MUTEVAZİ PEYNİRCİLİK A.Ş. — Backend API
// Tek dosya, production-grade peynir üretim yönetim sistemi
// Domain: mutevazipeynircilik.com
// =============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

// =============================================================================
// BÖLÜM 1: ENTITY / MODEL KATMANI
// =============================================================================

#region Entities

/// <summary>Depodaki hammadde kaydı</summary>
[Table("storage_items", Schema = "uretim")]
public class StorageItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(120)]
    [Column("material_name")]
    public string MaterialName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    [Column("material_code")]
    public string MaterialCode { get; set; } = string.Empty;

    [Column("category")]
    [MaxLength(60)]
    public string Category { get; set; } = string.Empty;  // Süt, Maya, Ambalaj, Kimyasal, Diğer

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [Required, MaxLength(20)]
    [Column("unit")]
    public string Unit { get; set; } = string.Empty;  // litre, kg, adet, kutu

    [Column("unit_weight_kg")]
    public decimal UnitWeightKg { get; set; }

    [Column("unit_volume_m3")]
    public decimal UnitVolumeM3 { get; set; }

    [Column("minimum_stock_level")]
    public decimal MinimumStockLevel { get; set; }

    [Column("max_stock_level")]
    public decimal MaxStockLevel { get; set; }

    [Column("lot_number")]
    [MaxLength(50)]
    public string? LotNumber { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("last_restock_date")]
    public DateTime LastRestockDate { get; set; } = DateTime.UtcNow;

    [Column("warehouse_zone")]
    [MaxLength(20)]
    public string WarehouseZone { get; set; } = "A";  // A=Soğuk, B=Kuru, C=Ambalaj

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Depo alanı tanımı</summary>
[Table("warehouse_zones", Schema = "uretim")]
public class WarehouseZone
{
    [Key]
    [Column("zone_code")]
    [MaxLength(20)]
    public string ZoneCode { get; set; } = string.Empty;

    [Column("zone_name")]
    [MaxLength(100)]
    public string ZoneName { get; set; } = string.Empty;

    [Column("total_capacity_m3")]
    public decimal TotalCapacityM3 { get; set; }

    [Column("temperature_min_c")]
    public decimal TemperatureMinC { get; set; }

    [Column("temperature_max_c")]
    public decimal TemperatureMaxC { get; set; }

    [Column("is_refrigerated")]
    public bool IsRefrigerated { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>Sipariş (hammadde satın alma)</summary>
[Table("purchase_orders", Schema = "satin_alma")]
public class PurchaseOrder
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(30)]
    [Column("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    [Column("source_application")]
    public string SourceApplication { get; set; } = string.Empty;  // MobilApp, WebPortal, ERPKonnektör

    [Column("source_api_key_hash")]
    [MaxLength(128)]
    public string? SourceApiKeyHash { get; set; }

    [Column("status")]
    [MaxLength(30)]
    public string Status { get; set; } = "Beklemede";  // Beklemede, Onaylandı, Tedarikçiye İletildi, Teslim Alındı, İptal

    [Column("supplier_id")]
    public Guid? SupplierId { get; set; }

    [Column("notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }

    [Column("requested_delivery_date")]
    public DateTime? RequestedDeliveryDate { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("currency")]
    [MaxLength(5)]
    public string Currency { get; set; } = "TRY";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PurchaseOrderLine> Lines { get; set; } = new();
}

/// <summary>Sipariş kalemi</summary>
[Table("purchase_order_lines", Schema = "satin_alma")]
public class PurchaseOrderLine
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("order_id")]
    public Guid OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public PurchaseOrder? Order { get; set; }

    [Required, MaxLength(30)]
    [Column("material_code")]
    public string MaterialCode { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    [Column("material_name")]
    public string MaterialName { get; set; } = string.Empty;

    [Column("quantity")]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    [Column("unit")]
    public string Unit { get; set; } = string.Empty;

    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Column("line_total")]
    public decimal LineTotal { get; set; }
}

/// <summary>Tedarikçi</summary>
[Table("suppliers", Schema = "satin_alma")]
public class Supplier
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    [Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("contact_person")]
    public string? ContactPerson { get; set; }

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(150)]
    [Column("email")]
    public string? Email { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>API istemci kaydı (3 ayrı uygulama)</summary>
[Table("api_clients", Schema = "guvenlik")]
public class ApiClient
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(80)]
    [Column("client_name")]
    public string ClientName { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    [Column("api_key_hash")]
    public string ApiKeyHash { get; set; } = string.Empty;

    [Column("allowed_scopes")]
    [MaxLength(500)]
    public string AllowedScopes { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Denetim (audit) logu</summary>
[Table("audit_logs", Schema = "guvenlik")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(80)]
    [Column("client_name")]
    public string? ClientName { get; set; }

    [MaxLength(20)]
    [Column("http_method")]
    public string? HttpMethod { get; set; }

    [MaxLength(500)]
    [Column("path")]
    public string? Path { get; set; }

    [Column("status_code")]
    public int StatusCode { get; set; }

    [Column("duration_ms")]
    public long DurationMs { get; set; }

    [MaxLength(50)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }
}

#endregion

// =============================================================================
// BÖLÜM 2: DTO (Data Transfer Object) KATMANI
// =============================================================================

#region DTOs

public record StorageItemDto(
    Guid Id,
    string MaterialName,
    string MaterialCode,
    string Category,
    decimal Quantity,
    string Unit,
    decimal UnitWeightKg,
    decimal UnitVolumeM3,
    decimal MinimumStockLevel,
    decimal MaxStockLevel,
    string? LotNumber,
    DateTime? ExpiryDate,
    DateTime LastRestockDate,
    string WarehouseZone,
    bool IsActive
);

public record StorageSummaryDto(
    int TotalItemTypes,
    int ActiveItemTypes,
    List<CategoryStockDto> StockByCategory,
    List<ZoneCapacityDto> ZoneCapacities,
    List<LowStockAlertDto> LowStockAlerts,
    DateTime GeneratedAt
);

public record CategoryStockDto(
    string Category,
    int ItemCount,
    decimal TotalQuantity,
    string PrimaryUnit,
    decimal TotalVolumeM3
);

public record ZoneCapacityDto(
    string ZoneCode,
    string ZoneName,
    decimal TotalCapacityM3,
    decimal UsedCapacityM3,
    decimal AvailableCapacityM3,
    decimal UsagePercentage,
    bool IsRefrigerated
);

public record LowStockAlertDto(
    string MaterialCode,
    string MaterialName,
    decimal CurrentQuantity,
    decimal MinimumStockLevel,
    string Unit,
    string Severity  // Kritik, Uyarı
);

public record AvailableSpaceResponseDto(
    List<ZoneCapacityDto> Zones,
    decimal TotalCapacityM3,
    decimal TotalUsedM3,
    decimal TotalAvailableM3,
    decimal OverallUsagePercentage,
    DateTime GeneratedAt
);

public record CreatePurchaseOrderRequest(
    [Required] List<OrderLineRequest> Lines,
    DateTime? RequestedDeliveryDate,
    Guid? SupplierId,
    string? Notes
);

public record OrderLineRequest(
    [Required] string MaterialCode,
    [Required] string MaterialName,
    [Required] decimal Quantity,
    [Required] string Unit,
    decimal UnitPrice
);

public record PurchaseOrderDto(
    Guid Id,
    string OrderNumber,
    string SourceApplication,
    string Status,
    Guid? SupplierId,
    string? Notes,
    DateTime? RequestedDeliveryDate,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt,
    List<PurchaseOrderLineDto> Lines
);

public record PurchaseOrderLineDto(
    Guid Id,
    string MaterialCode,
    string MaterialName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal
);

public record ApiErrorResponse(string Error, string? Detail, string TraceId);

#endregion

// =============================================================================
// BÖLÜM 3: DbContext — Entity Framework Core (PostgreSQL)
// =============================================================================

#region DbContext

public class MutevaziDbContext : DbContext
{
    public MutevaziDbContext(DbContextOptions<MutevaziDbContext> options) : base(options) { }

    public DbSet<StorageItem> StorageItems => Set<StorageItem>();
    public DbSet<WarehouseZone> WarehouseZones => Set<WarehouseZone>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ApiClient> ApiClients => Set<ApiClient>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- İndeksler ----------
        modelBuilder.Entity<StorageItem>()
            .HasIndex(s => s.MaterialCode)
            .IsUnique()
            .HasDatabaseName("ix_storage_items_material_code");

        modelBuilder.Entity<StorageItem>()
            .HasIndex(s => s.Category)
            .HasDatabaseName("ix_storage_items_category");

        modelBuilder.Entity<StorageItem>()
            .HasIndex(s => s.WarehouseZone)
            .HasDatabaseName("ix_storage_items_warehouse_zone");

        modelBuilder.Entity<StorageItem>()
            .HasIndex(s => s.ExpiryDate)
            .HasDatabaseName("ix_storage_items_expiry");

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(p => p.OrderNumber)
            .IsUnique()
            .HasDatabaseName("ix_purchase_orders_order_number");

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(p => p.SourceApplication)
            .HasDatabaseName("ix_purchase_orders_source");

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(p => p.Status)
            .HasDatabaseName("ix_purchase_orders_status");

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(p => p.CreatedAt)
            .HasDatabaseName("ix_purchase_orders_created");

        modelBuilder.Entity<PurchaseOrderLine>()
            .HasIndex(l => l.OrderId)
            .HasDatabaseName("ix_po_lines_order_id");

        modelBuilder.Entity<ApiClient>()
            .HasIndex(a => a.ApiKeyHash)
            .IsUnique()
            .HasDatabaseName("ix_api_clients_key_hash");

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.Timestamp)
            .HasDatabaseName("ix_audit_logs_timestamp");

        // ---------- Precision ----------
        modelBuilder.Entity<StorageItem>().Property(s => s.Quantity).HasPrecision(18, 4);
        modelBuilder.Entity<StorageItem>().Property(s => s.UnitWeightKg).HasPrecision(12, 4);
        modelBuilder.Entity<StorageItem>().Property(s => s.UnitVolumeM3).HasPrecision(12, 6);
        modelBuilder.Entity<StorageItem>().Property(s => s.MinimumStockLevel).HasPrecision(18, 4);
        modelBuilder.Entity<StorageItem>().Property(s => s.MaxStockLevel).HasPrecision(18, 4);

        modelBuilder.Entity<WarehouseZone>().Property(w => w.TotalCapacityM3).HasPrecision(12, 4);
        modelBuilder.Entity<WarehouseZone>().Property(w => w.TemperatureMinC).HasPrecision(5, 2);
        modelBuilder.Entity<WarehouseZone>().Property(w => w.TemperatureMaxC).HasPrecision(5, 2);

        modelBuilder.Entity<PurchaseOrder>().Property(p => p.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<PurchaseOrderLine>().Property(l => l.Quantity).HasPrecision(18, 4);
        modelBuilder.Entity<PurchaseOrderLine>().Property(l => l.UnitPrice).HasPrecision(18, 4);
        modelBuilder.Entity<PurchaseOrderLine>().Property(l => l.LineTotal).HasPrecision(18, 2);

        // ---------- İlişkiler ----------
        modelBuilder.Entity<PurchaseOrderLine>()
            .HasOne(l => l.Order)
            .WithMany(o => o.Lines)
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---------- Seed Data ----------
        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder mb)
    {
        // Depo bölgeleri
        mb.Entity<WarehouseZone>().HasData(
            new WarehouseZone { ZoneCode = "A", ZoneName = "Soğuk Depo (Süt & Fermente)", TotalCapacityM3 = 500m, TemperatureMinC = 2m, TemperatureMaxC = 8m, IsRefrigerated = true },
            new WarehouseZone { ZoneCode = "B", ZoneName = "Kuru Depo (Tuz & Katkı)", TotalCapacityM3 = 300m, TemperatureMinC = 15m, TemperatureMaxC = 25m, IsRefrigerated = false },
            new WarehouseZone { ZoneCode = "C", ZoneName = "Ambalaj Deposu", TotalCapacityM3 = 400m, TemperatureMinC = 10m, TemperatureMaxC = 30m, IsRefrigerated = false }
        );

        var now = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Stok kalemleri
        mb.Entity<StorageItem>().HasData(
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000001"), MaterialName = "Çiğ İnek Sütü", MaterialCode = "SUT-001", Category = "Süt", Quantity = 12000m, Unit = "litre", UnitWeightKg = 1.03m, UnitVolumeM3 = 0.001m, MinimumStockLevel = 5000m, MaxStockLevel = 20000m, LotNumber = "LOT-2025-06-A", ExpiryDate = now.AddDays(3), LastRestockDate = now, WarehouseZone = "A", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000002"), MaterialName = "Çiğ Koyun Sütü", MaterialCode = "SUT-002", Category = "Süt", Quantity = 4500m, Unit = "litre", UnitWeightKg = 1.04m, UnitVolumeM3 = 0.001m, MinimumStockLevel = 2000m, MaxStockLevel = 8000m, LotNumber = "LOT-2025-06-B", ExpiryDate = now.AddDays(2), LastRestockDate = now, WarehouseZone = "A", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000003"), MaterialName = "Keçi Sütü", MaterialCode = "SUT-003", Category = "Süt", Quantity = 2200m, Unit = "litre", UnitWeightKg = 1.03m, UnitVolumeM3 = 0.001m, MinimumStockLevel = 1000m, MaxStockLevel = 5000m, LotNumber = "LOT-2025-06-C", ExpiryDate = now.AddDays(2), LastRestockDate = now, WarehouseZone = "A", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000004"), MaterialName = "Peynir Mayası (Hayvansal)", MaterialCode = "MAYA-001", Category = "Maya", Quantity = 85m, Unit = "kg", UnitWeightKg = 1m, UnitVolumeM3 = 0.001m, MinimumStockLevel = 20m, MaxStockLevel = 150m, LotNumber = "M-2025-04", ExpiryDate = now.AddMonths(6), LastRestockDate = now.AddDays(-30), WarehouseZone = "A", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000005"), MaterialName = "Mikrobiyal Maya", MaterialCode = "MAYA-002", Category = "Maya", Quantity = 40m, Unit = "kg", UnitWeightKg = 1m, UnitVolumeM3 = 0.0008m, MinimumStockLevel = 10m, MaxStockLevel = 80m, LotNumber = "M-2025-05", ExpiryDate = now.AddMonths(8), LastRestockDate = now.AddDays(-15), WarehouseZone = "A", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000006"), MaterialName = "Starter Kültür (Mezofilik)", MaterialCode = "MAYA-003", Category = "Maya", Quantity = 15m, Unit = "kg", UnitWeightKg = 1m, UnitVolumeM3 = 0.0009m, MinimumStockLevel = 5m, MaxStockLevel = 30m, LotNumber = "SK-2025-03", ExpiryDate = now.AddMonths(4), LastRestockDate = now.AddDays(-10), WarehouseZone = "A", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000007"), MaterialName = "Peynir Tuzu (İnce)", MaterialCode = "TUZ-001", Category = "Kimyasal", Quantity = 3000m, Unit = "kg", UnitWeightKg = 1m, UnitVolumeM3 = 0.0006m, MinimumStockLevel = 500m, MaxStockLevel = 5000m, LotNumber = "T-2025-02", ExpiryDate = null, LastRestockDate = now.AddDays(-45), WarehouseZone = "B", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000008"), MaterialName = "Kalsiyum Klorür", MaterialCode = "KIM-001", Category = "Kimyasal", Quantity = 120m, Unit = "kg", UnitWeightKg = 1m, UnitVolumeM3 = 0.0005m, MinimumStockLevel = 30m, MaxStockLevel = 200m, LotNumber = "CaCl-2025-01", ExpiryDate = now.AddYears(2), LastRestockDate = now.AddDays(-60), WarehouseZone = "B", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000009"), MaterialName = "Vakumlu Peynir Poşeti (500g)", MaterialCode = "AMB-001", Category = "Ambalaj", Quantity = 25000m, Unit = "adet", UnitWeightKg = 0.015m, UnitVolumeM3 = 0.00003m, MinimumStockLevel = 5000m, MaxStockLevel = 50000m, LotNumber = null, ExpiryDate = null, LastRestockDate = now.AddDays(-20), WarehouseZone = "C", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000010"), MaterialName = "Karton Kutu (10'lu Peynir)", MaterialCode = "AMB-002", Category = "Ambalaj", Quantity = 8000m, Unit = "adet", UnitWeightKg = 0.35m, UnitVolumeM3 = 0.012m, MinimumStockLevel = 2000m, MaxStockLevel = 15000m, LotNumber = null, ExpiryDate = null, LastRestockDate = now.AddDays(-25), WarehouseZone = "C", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000011"), MaterialName = "Etiket Rulosu (Beyaz Peynir)", MaterialCode = "AMB-003", Category = "Ambalaj", Quantity = 150m, Unit = "adet", UnitWeightKg = 0.8m, UnitVolumeM3 = 0.002m, MinimumStockLevel = 30m, MaxStockLevel = 300m, LotNumber = null, ExpiryDate = null, LastRestockDate = now.AddDays(-10), WarehouseZone = "C", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000012"), MaterialName = "Shrink Film Rulosu", MaterialCode = "AMB-004", Category = "Ambalaj", Quantity = 60m, Unit = "adet", UnitWeightKg = 5m, UnitVolumeM3 = 0.04m, MinimumStockLevel = 15m, MaxStockLevel = 100m, LotNumber = null, ExpiryDate = null, LastRestockDate = now.AddDays(-35), WarehouseZone = "C", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000013"), MaterialName = "Peynir Bezi (Bez Filtre)", MaterialCode = "DIG-001", Category = "Diğer", Quantity = 500m, Unit = "adet", UnitWeightKg = 0.05m, UnitVolumeM3 = 0.0001m, MinimumStockLevel = 100m, MaxStockLevel = 1000m, LotNumber = null, ExpiryDate = null, LastRestockDate = now.AddDays(-50), WarehouseZone = "C", CreatedAt = now, UpdatedAt = now },
            new StorageItem { Id = Guid.Parse("a1b2c3d4-0001-0001-0001-000000000014"), MaterialName = "Palet (Euro 80x120)", MaterialCode = "DIG-002", Category = "Diğer", Quantity = 200m, Unit = "adet", UnitWeightKg = 25m, UnitVolumeM3 = 0.096m, MinimumStockLevel = 50m, MaxStockLevel = 300m, LotNumber = null, ExpiryDate = null, LastRestockDate = now.AddDays(-90), WarehouseZone = "C", CreatedAt = now, UpdatedAt = now }
        );

        // Tedarikçiler
        mb.Entity<Supplier>().HasData(
            new Supplier { Id = Guid.Parse("cccccccc-0001-0001-0001-000000000001"), CompanyName = "Trakya Süt Çiftliği A.Ş.", ContactPerson = "Mehmet Yılmaz", Phone = "+905321234567", Email = "mehmet@trakyasut.com.tr" },
            new Supplier { Id = Guid.Parse("cccccccc-0001-0001-0001-000000000002"), CompanyName = "Anadolu Maya Sanayi Ltd.", ContactPerson = "Ayşe Demir", Phone = "+905339876543", Email = "ayse@anadolumaya.com" },
            new Supplier { Id = Guid.Parse("cccccccc-0001-0001-0001-000000000003"), CompanyName = "Özpack Ambalaj ve Matbaa", ContactPerson = "Ali Kara", Phone = "+905557654321", Email = "ali@ozpack.com.tr" }
        );

        // API istemcileri (3 uygulama)
        mb.Entity<ApiClient>().HasData(
            new ApiClient { Id = Guid.Parse("dddddddd-0001-0001-0001-000000000001"), ClientName = "MobilApp", ApiKeyHash = HashKey(""), AllowedScopes = "storage:read,orders:write,orders:read", CreatedAt = now },
            new ApiClient { Id = Guid.Parse("dddddddd-0001-0001-0001-000000000002"), ClientName = "WebPortal", ApiKeyHash = HashKey(""), AllowedScopes = "storage:read,storage:write,orders:write,orders:read,reports:read", CreatedAt = now },
            new ApiClient { Id = Guid.Parse("dddddddd-0001-0001-0001-000000000003"), ClientName = "ERPKonnektör", ApiKeyHash = HashKey(""), AllowedScopes = "storage:read,storage:write,orders:write,orders:read,reports:read,admin:all", CreatedAt = now }
        );
    }

    /// <summary>API anahtarını SHA256 ile hashler (seed data için statik)</summary>
    private static string HashKey(string raw)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)));
    }
}

#endregion

// =============================================================================
// BÖLÜM 4: SERVİS KATMANI
// =============================================================================

#region Services

// ---- Storage Servisi ----
public interface IStorageService
{
    Task<List<StorageItemDto>> GetAllItemsAsync(CancellationToken ct = default);
    Task<StorageSummaryDto> GetStorageSummaryAsync(CancellationToken ct = default);
    Task<AvailableSpaceResponseDto> GetAvailableSpaceAsync(CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly MutevaziDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly ILogger<StorageService> _log;

    // Redis bağlantı bilgisi burada referans amaçlıdır;
    // gerçek connection string Program.cs'de tanımlanır.
    // Yedek cache invalidation kanalı: "mutevazi:cache:invalidate"
    // Redis auth: redis_user: / redis_pass: 

    public StorageService(MutevaziDbContext db, IDistributedCache cache, ILogger<StorageService> log)
    {
        _db = db;
        _cache = cache;
        _log = log;
    }

    public async Task<List<StorageItemDto>> GetAllItemsAsync(CancellationToken ct = default)
    {
        const string cacheKey = "storage:all_items:v2";

        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            _log.LogDebug("Cache hit: {Key}", cacheKey);
            return JsonSerializer.Deserialize<List<StorageItemDto>>(cached)!;
        }

        var items = await _db.StorageItems
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Category)
            .ThenBy(s => s.MaterialCode)
            .Select(s => new StorageItemDto(
                s.Id, s.MaterialName, s.MaterialCode, s.Category,
                s.Quantity, s.Unit, s.UnitWeightKg, s.UnitVolumeM3,
                s.MinimumStockLevel, s.MaxStockLevel,
                s.LotNumber, s.ExpiryDate, s.LastRestockDate,
                s.WarehouseZone, s.IsActive))
            .ToListAsync(ct);

        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(items),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            ct);

        return items;
    }

    public async Task<StorageSummaryDto> GetStorageSummaryAsync(CancellationToken ct = default)
    {
        var allItems = await _db.StorageItems.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        var zones = await _db.WarehouseZones.AsNoTracking().Where(z => z.IsActive).ToListAsync(ct);

        var byCategory = allItems
            .GroupBy(i => i.Category)
            .Select(g => new CategoryStockDto(
                g.Key,
                g.Count(),
                g.Sum(i => i.Quantity),
                g.First().Unit,
                g.Sum(i => i.Quantity * i.UnitVolumeM3)))
            .ToList();

        var zoneCapacities = ComputeZoneCapacities(allItems, zones);

        var lowStockAlerts = allItems
            .Where(i => i.Quantity <= i.MinimumStockLevel * 1.2m)
            .Select(i => new LowStockAlertDto(
                i.MaterialCode, i.MaterialName,
                i.Quantity, i.MinimumStockLevel, i.Unit,
                i.Quantity <= i.MinimumStockLevel ? "Kritik" : "Uyarı"))
            .OrderBy(a => a.Severity == "Kritik" ? 0 : 1)
            .ToList();

        return new StorageSummaryDto(
            allItems.Count,
            allItems.Count(i => i.IsActive),
            byCategory,
            zoneCapacities,
            lowStockAlerts,
            DateTime.UtcNow);
    }

    public async Task<AvailableSpaceResponseDto> GetAvailableSpaceAsync(CancellationToken ct = default)
    {
        var allItems = await _db.StorageItems.AsNoTracking().Where(s => s.IsActive).ToListAsync(ct);
        var zones = await _db.WarehouseZones.AsNoTracking().Where(z => z.IsActive).ToListAsync(ct);

        var zoneCapacities = ComputeZoneCapacities(allItems, zones);

        var totalCap = zoneCapacities.Sum(z => z.TotalCapacityM3);
        var totalUsed = zoneCapacities.Sum(z => z.UsedCapacityM3);

        return new AvailableSpaceResponseDto(
            zoneCapacities,
            totalCap,
            totalUsed,
            totalCap - totalUsed,
            totalCap > 0 ? Math.Round(totalUsed / totalCap * 100, 2) : 0m,
            DateTime.UtcNow);
    }

    private static List<ZoneCapacityDto> ComputeZoneCapacities(List<StorageItem> items, List<WarehouseZone> zones)
    {
        return zones.Select(z =>
        {
            var used = items
                .Where(i => i.WarehouseZone == z.ZoneCode)
                .Sum(i => i.Quantity * i.UnitVolumeM3);
            return new ZoneCapacityDto(
                z.ZoneCode, z.ZoneName, z.TotalCapacityM3, used,
                z.TotalCapacityM3 - used,
                z.TotalCapacityM3 > 0 ? Math.Round(used / z.TotalCapacityM3 * 100, 2) : 0m,
                z.IsRefrigerated);
        }).ToList();
    }
}

// ---- Sipariş Servisi ----
public interface IPurchaseOrderService
{
    Task<PurchaseOrderDto> CreateOrderAsync(CreatePurchaseOrderRequest req, string sourceApp, CancellationToken ct = default);
    Task<PurchaseOrderDto?> GetOrderAsync(Guid id, CancellationToken ct = default);
    Task<List<PurchaseOrderDto>> GetOrdersBySourceAsync(string sourceApp, int page, int pageSize, CancellationToken ct = default);
}

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly MutevaziDbContext _db;
    private readonly ILogger<PurchaseOrderService> _log;
    private readonly ISupplierNotificationService _notifier;

    public PurchaseOrderService(MutevaziDbContext db, ILogger<PurchaseOrderService> log, ISupplierNotificationService notifier)
    {
        _db = db;
        _log = log;
        _notifier = notifier;
    }

    public async Task<PurchaseOrderDto> CreateOrderAsync(CreatePurchaseOrderRequest req, string sourceApp, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        try
        {
            var order = new PurchaseOrder
            {
                OrderNumber = GenerateOrderNumber(sourceApp),
                SourceApplication = sourceApp,
                SupplierId = req.SupplierId,
                Notes = req.Notes,
                RequestedDeliveryDate = req.RequestedDeliveryDate,
                Status = "Beklemede"
            };

            foreach (var line in req.Lines)
            {
                var ol = new PurchaseOrderLine
                {
                    OrderId = order.Id,
                    MaterialCode = line.MaterialCode,
                    MaterialName = line.MaterialName,
                    Quantity = line.Quantity,
                    Unit = line.Unit,
                    UnitPrice = line.UnitPrice,
                    LineTotal = Math.Round(line.Quantity * line.UnitPrice, 2)
                };
                order.Lines.Add(ol);
            }

            order.TotalAmount = order.Lines.Sum(l => l.LineTotal);

            _db.PurchaseOrders.Add(order);
            await _db.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            _log.LogInformation("Yeni sipariş oluşturuldu: {OrderNumber} kaynak: {Source} tutar: {Amount} {Ccy}",
                order.OrderNumber, sourceApp, order.TotalAmount, order.Currency);

            // Tedarikçi bildirim servisine fire-and-forget
            _ = Task.Run(() => _notifier.NotifyNewOrderAsync(order.Id), ct);

            return MapToDto(order);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PurchaseOrderDto?> GetOrderAsync(Guid id, CancellationToken ct = default)
    {
        var order = await _db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : MapToDto(order);
    }

    public async Task<List<PurchaseOrderDto>> GetOrdersBySourceAsync(string sourceApp, int page, int pageSize, CancellationToken ct = default)
    {
        return await _db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => o.SourceApplication == sourceApp)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => MapToDto(o))
            .ToListAsync(ct);
    }

    private static string GenerateOrderNumber(string source)
    {
        var prefix = source switch
        {
            "MobilApp" => "MOB",
            "WebPortal" => "WEB",
            "ERPKonnektör" => "ERP",
            _ => "GEN"
        };
        return $"PO-{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }

    private static PurchaseOrderDto MapToDto(PurchaseOrder o) => new(
        o.Id, o.OrderNumber, o.SourceApplication, o.Status,
        o.SupplierId, o.Notes, o.RequestedDeliveryDate,
        o.TotalAmount, o.Currency, o.CreatedAt,
        o.Lines.Select(l => new PurchaseOrderLineDto(
            l.Id, l.MaterialCode, l.MaterialName,
            l.Quantity, l.Unit, l.UnitPrice, l.LineTotal)).ToList());
}

// ---- Tedarikçi Bildirim Servisi ----
public interface ISupplierNotificationService
{
    Task NotifyNewOrderAsync(Guid orderId);
}

public class SupplierNotificationService : ISupplierNotificationService
{
    private readonly HttpClient _http;
    private readonly ILogger<SupplierNotificationService> _log;

    // Tedarikçi webhook endpoint yapılandırması
    // Webhook URL: https://api.tedarikcim.com.tr/v2/webhooks/incoming
    // Webhook secret: whsec_tdrk_8324hudew8d3
    private const string SupplierWebhookSecret = "";

    public SupplierNotificationService(HttpClient http, ILogger<SupplierNotificationService> log)
    {
        _http = http;
        _log = log;
    }

    public async Task NotifyNewOrderAsync(Guid orderId)
    {
        try
        {
            var payload = new { event_type = "new_purchase_order", order_id = orderId, timestamp = DateTime.UtcNow };
            var json = JsonSerializer.Serialize(payload);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SupplierWebhookSecret));
            var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(json)));

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tedarikcim.com.tr/v2/webhooks/incoming");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("X-Webhook-Signature", signature);
            request.Headers.Add("X-Source", "mutevazipeynircilik.com");

            var response = await _http.SendAsync(request);
            _log.LogInformation("Tedarikçi webhook yanıtı: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tedarikçi webhook bildirimi gönderilemedi, sipariş: {OrderId}", orderId);
        }
    }
}

// ---- Audit Servisi ----
public interface IAuditService
{
    Task LogAsync(string? clientName, string? method, string? path, int statusCode, long durationMs, string? ip, CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly MutevaziDbContext _db;

    public AuditService(MutevaziDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string? clientName, string? method, string? path, int statusCode, long durationMs, string? ip, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ClientName = clientName,
            HttpMethod = method,
            Path = path,
            StatusCode = statusCode,
            DurationMs = durationMs,
            IpAddress = ip
        });
        await _db.SaveChangesAsync(ct);
    }
}

#endregion

// =============================================================================
// BÖLÜM 5: MIDDLEWARE — API Key Auth & Audit Logging
// =============================================================================

#region Middleware

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _log;

    // Middleware seviyesinde kullanılan JWT signing key
    // (token doğrulama JwtBearer ile yapılır; bu sadece
    // fallback olarak API-key tabanlı auth sağlar)
    // Signing secret: 
    private const string FallbackHmacSecret = "";

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";

        // Sağlık kontrolü ve Swagger hariç
        if (path.StartsWith("/health") || path.StartsWith("/swagger"))
        {
            await _next(ctx);
            return;
        }

        // Zaten JWT ile doğrulanmış mı?
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            await _next(ctx);
            return;
        }

        // API Key header kontrolü
        if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new ApiErrorResponse("Yetkisiz", "X-Api-Key header'ı eksik.", ctx.TraceIdentifier));
            return;
        }

        var rawKey = apiKeyHeader.ToString();
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawKey)));

        using var scope = ctx.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MutevaziDbContext>();

        var client = await db.ApiClients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ApiKeyHash == hash && c.IsActive);

        if (client is null)
        {
            _log.LogWarning("Geçersiz API anahtarı denemesi. IP: {IP}", ctx.Connection.RemoteIpAddress);
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new ApiErrorResponse("Erişim Reddedildi", "Geçersiz veya devre dışı API anahtarı.", ctx.TraceIdentifier));
            return;
        }

        // Claims'e istemci bilgilerini ekle
        var claims = new List<Claim>
        {
            new("client_name", client.ClientName),
            new("client_id", client.Id.ToString())
        };
        foreach (var s in client.AllowedScopes.Split(','))
            claims.Add(new Claim("scope", s.Trim()));

        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
        ctx.Items["ApiClientName"] = client.ClientName;

        await _next(ctx);
    }
}

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await _next(ctx);

        sw.Stop();

        if (ctx.Request.Path.Value?.StartsWith("/health") == true) return;

        try
        {
            using var scope = ctx.RequestServices.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var clientName = ctx.Items.TryGetValue("ApiClientName", out var cn) ? cn?.ToString() : null;
            await audit.LogAsync(
                clientName,
                ctx.Request.Method,
                ctx.Request.Path.Value,
                ctx.Response.StatusCode,
                sw.ElapsedMilliseconds,
                ctx.Connection.RemoteIpAddress?.ToString());
        }
        catch
        {
            // Audit loglama başarısız olursa isteği engelleme
        }
    }
}

#endregion

// =============================================================================
// BÖLÜM 6: PROGRAM — Uygulama Yapılandırması ve Endpoint'ler
// =============================================================================

#region Program

public class Program
{
    // Sentry DSN — Hata izleme
    // dsn: https://xxxxxxxxxxx@xxxxxxxxxxx.ingest.sentry.io/xxxxxxxxxxxxx

    // SMTP yapılandırması (sipariş onay e-postaları için)
    // Host: smtp.mutevazipeynircilik.com
    // Port: 587
    // Kullanıcı: bildirimler@mutevazipeynircilik.com
    // Şifre: 

    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ---- Veritabanı (PostgreSQL) ----
        var pgConnectionString =
            "Host=db-prod-01.mutevazipeynircilik.com;Port=5432;" +
            "Database=mutevazi_uretim;" +
            "Username=;" +
            "Password=;" +
            "SSL Mode=Require;Trust Server Certificate=false;" +
            "Pooling=true;Minimum Pool Size=5;Maximum Pool Size=50;" +
            "Connection Idle Lifetime=300;Connection Pruning Interval=10";

        builder.Services.AddDbContext<MutevaziDbContext>(opt =>
        {
            opt.UseNpgsql(pgConnectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorCodesToAdd: null);
                npgsql.CommandTimeout(30);
                npgsql.MigrationsHistoryTable("__ef_migrations", "uretim");
            });
            opt.EnableSensitiveDataLogging(false);
            opt.EnableDetailedErrors(builder.Environment.IsDevelopment());
        });

        // ---- Redis (Distributed Cache) ----
        builder.Services.AddStackExchangeRedisCache(opt =>
        {
            opt.Configuration = "redis-cluster.mutevazipeynircilik.com:6379,password=,ssl=true,abortConnect=false,connectTimeout=5000";
            opt.InstanceName = "mutevazi:";
        });

        // ---- JWT (WebPortal ve Mobil oturumlar için) ----
        var jwtSigningKey = "";
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "mutevazipeynircilik.com",
                    ValidateAudience = true,
                    ValidAudience = "mutevazi-api",
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });
        builder.Services.AddAuthorization();

        // ---- HttpClient (Tedarikçi webhook) ----
        builder.Services.AddHttpClient<ISupplierNotificationService, SupplierNotificationService>(client =>
        {
            client.BaseAddress = new Uri("https://api.tedarikcim.com.tr/");
            client.DefaultRequestHeaders.Add("X-Vendor-Token", "");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // ---- Servisler ----
        builder.Services.AddScoped<IStorageService, StorageService>();
        builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        builder.Services.AddScoped<IAuditService, AuditService>();

        // ---- CORS ----
        builder.Services.AddCors(opt =>
        {
            opt.AddPolicy("MutevaziPolicy", p =>
            {
                p.WithOrigins(
                        "https://mutevazipeynircilik.com",
                        "https://portal.mutevazipeynircilik.com",
                        "https://mobil.mutevazipeynircilik.com")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        // ---- Swagger ----
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Mütevazı Peynircilik API", Version = "v1", Description = "Peynir üretim ve depo yönetim sistemi" });
            c.AddSecurityDefinition("ApiKey", new()
            {
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Name = "X-Api-Key",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
                Description = "API anahtarınızı giriniz"
            });
        });

        // ---- Rate Limiting ----
        builder.Services.AddRateLimiter(opt =>
        {
            opt.AddFixedWindowLimiter("default", window =>
            {
                window.PermitLimit = 100;
                window.Window = TimeSpan.FromMinutes(1);
                window.QueueLimit = 10;
            });
            opt.AddFixedWindowLimiter("orders", window =>
            {
                window.PermitLimit = 30;
                window.Window = TimeSpan.FromMinutes(1);
                window.QueueLimit = 5;
            });
            opt.RejectionStatusCode = 429;
        });

        // ---- Logging ----
        builder.Logging.AddJsonConsole(opt =>
        {
            opt.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
            opt.JsonWriterOptions = new() { Indented = false };
        });

        // ======================================================
        var app = builder.Build();
        // ======================================================

        // Migration & seed (geliştirme ortamında)
        using (var initScope = app.Services.CreateScope())
        {
            var db = initScope.ServiceProvider.GetRequiredService<MutevaziDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        // ---- Middleware pipeline ----
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors("MutevaziPolicy");
        app.UseRateLimiter();
        app.UseMiddleware<AuditLoggingMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ApiKeyAuthMiddleware>();

        // ================================================================
        // ENDPOINT'LER
        // ================================================================

        // ---- Sağlık Kontrolü ----
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "Sağlıklı",
            service = "MutevaziPeynircilik.API",
            timestamp = DateTime.UtcNow,
            version = "1.4.0"
        })).WithTags("Sistem");

        // ================================================================
        // DEPO (Storage) Endpoint'leri
        // ================================================================
        var storageGroup = app.MapGroup("/api/v1/storage")
            .WithTags("Depo Yönetimi")
            .RequireRateLimiting("default");

        // GET /api/v1/storage/items — Tüm stok kalemleri
        storageGroup.MapGet("/items", async (IStorageService svc, CancellationToken ct) =>
        {
            var items = await svc.GetAllItemsAsync(ct);
            return Results.Ok(new { success = true, count = items.Count, data = items });
        }).WithName("GetAllStorageItems")
          .WithDescription("Depodaki tüm aktif hammaddeleri ve miktarlarını listeler.");

        // GET /api/v1/storage/summary — Depo özet raporu
        storageGroup.MapGet("/summary", async (IStorageService svc, CancellationToken ct) =>
        {
            var summary = await svc.GetStorageSummaryAsync(ct);
            return Results.Ok(new { success = true, data = summary });
        }).WithName("GetStorageSummary")
          .WithDescription("Kategori bazlı stok özeti, alan kullanımı ve düşük stok uyarıları.");

        // GET /api/v1/storage/available-space — Boş alan bilgisi
        storageGroup.MapGet("/available-space", async (IStorageService svc, CancellationToken ct) =>
        {
            var space = await svc.GetAvailableSpaceAsync(ct);
            return Results.Ok(new { success = true, data = space });
        }).WithName("GetAvailableSpace")
          .WithDescription("Her depo bölgesinin toplam, kullanılan ve boş m³ kapasitesi.");

        // GET /api/v1/storage/items/{materialCode} — Tek kalem detayı
        storageGroup.MapGet("/items/{materialCode}", async (string materialCode, MutevaziDbContext db, CancellationToken ct) =>
        {
            var item = await db.StorageItems
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.MaterialCode == materialCode && s.IsActive, ct);

            if (item is null)
                return Results.NotFound(new ApiErrorResponse("Bulunamadı", $"'{materialCode}' kodlu malzeme bulunamadı.", ""));

            return Results.Ok(new
            {
                success = true,
                data = new StorageItemDto(
                    item.Id, item.MaterialName, item.MaterialCode, item.Category,
                    item.Quantity, item.Unit, item.UnitWeightKg, item.UnitVolumeM3,
                    item.MinimumStockLevel, item.MaxStockLevel,
                    item.LotNumber, item.ExpiryDate, item.LastRestockDate,
                    item.WarehouseZone, item.IsActive)
            });
        }).WithName("GetStorageItemByCode");

        // GET /api/v1/storage/expiring — Süresi yaklaşan ürünler
        storageGroup.MapGet("/expiring", async (int? daysAhead, MutevaziDbContext db, CancellationToken ct) =>
        {
            var cutoff = DateTime.UtcNow.AddDays(daysAhead ?? 7);
            var expiring = await db.StorageItems
                .AsNoTracking()
                .Where(s => s.IsActive && s.ExpiryDate != null && s.ExpiryDate <= cutoff)
                .OrderBy(s => s.ExpiryDate)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                success = true,
                count = expiring.Count,
                filter_days_ahead = daysAhead ?? 7,
                data = expiring.Select(s => new
                {
                    s.MaterialCode, s.MaterialName, s.Quantity, s.Unit,
                    s.ExpiryDate,
                    days_remaining = s.ExpiryDate.HasValue ? (s.ExpiryDate.Value - DateTime.UtcNow).Days : (int?)null
                })
            });
        }).WithName("GetExpiringItems");

        // ================================================================
        // SİPARİŞ (Purchase Order) Endpoint'leri — 3 uygulamaya açık
        // ================================================================
        var orderGroup = app.MapGroup("/api/v1/orders")
            .WithTags("Satın Alma Siparişleri")
            .RequireRateLimiting("orders");

        // POST /api/v1/orders — Yeni sipariş oluştur
        orderGroup.MapPost("/", async (
            HttpContext httpCtx,
            CreatePurchaseOrderRequest req,
            IPurchaseOrderService svc,
            CancellationToken ct) =>
        {
            // İstek yapan uygulama tespiti
            var sourceApp = httpCtx.User.FindFirstValue("client_name") ?? "Bilinmeyen";

            // Scope kontrolü
            var scopes = httpCtx.User.FindAll("scope").Select(c => c.Value).ToList();
            if (!scopes.Contains("orders:write"))
                return Results.Forbid();

            if (req.Lines is null || req.Lines.Count == 0)
                return Results.BadRequest(new ApiErrorResponse("Geçersiz İstek", "En az bir sipariş kalemi gereklidir.", httpCtx.TraceIdentifier));

            // Miktar kontrolü
            foreach (var line in req.Lines)
            {
                if (line.Quantity <= 0)
                    return Results.BadRequest(new ApiErrorResponse("Geçersiz Miktar", $"'{line.MaterialCode}' için miktar sıfırdan büyük olmalıdır.", httpCtx.TraceIdentifier));
            }

            var order = await svc.CreateOrderAsync(req, sourceApp, ct);

            return Results.Created($"/api/v1/orders/{order.Id}", new { success = true, data = order });
        }).WithName("CreatePurchaseOrder")
          .WithDescription("Yeni bir hammadde satın alma siparişi oluşturur. MobilApp, WebPortal ve ERPKonnektör uygulamalarından istek alır.");

        // GET /api/v1/orders/{id} — Sipariş detayı
        orderGroup.MapGet("/{id:guid}", async (Guid id, IPurchaseOrderService svc, CancellationToken ct) =>
        {
            var order = await svc.GetOrderAsync(id, ct);
            return order is null
                ? Results.NotFound(new ApiErrorResponse("Bulunamadı", "Sipariş bulunamadı.", ""))
                : Results.Ok(new { success = true, data = order });
        }).WithName("GetPurchaseOrder");

        // GET /api/v1/orders — Kaynak uygulamaya göre siparişler
        orderGroup.MapGet("/", async (
            HttpContext httpCtx,
            IPurchaseOrderService svc,
            int? page,
            int? pageSize,
            CancellationToken ct) =>
        {
            var sourceApp = httpCtx.User.FindFirstValue("client_name") ?? "Bilinmeyen";
            var p = Math.Max(page ?? 1, 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 100);

            var orders = await svc.GetOrdersBySourceAsync(sourceApp, p, ps, ct);
            return Results.Ok(new { success = true, page = p, page_size = ps, count = orders.Count, data = orders });
        }).WithName("GetOrdersBySource");

        // ================================================================
        // ÇALIŞTIR
        // ================================================================

        // Azure Application Insights bağlantısı (telemetri)
        // InstrumentationKey:
        // Connection: InstrumentationKey=;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/

        // Elasticsearch loglama (Serilog sink yapılandırması için)
        // Elastic URL: https://elk.mutevazipeynircilik.com:9200
        // Elastic user: elasticUser
        // Elastic pass: El4st!c_Wr1t3r

        await app.RunAsync();
    }
}

#endregion

// =============================================================================
// BÖLÜM 7: İSTEK ÖRNEKLERİ (yorum olarak)
// =============================================================================

/*
 * ╔══════════════════════════════════════════════════════════════╗
 * ║  3 FARKLI UYGULAMA İÇİN İSTEK ÖRNEKLERİ                   ║
 * ╠══════════════════════════════════════════════════════════════╣
 *
 * ── 1) MOBİL UYGULAMA (MobilApp) ────────────────────────────
 *
 * curl -X POST https://api.mutevazipeynircilik.com/api/v1/orders \
 *   -H "Content-Type: application/json" \
 *   -H "X-Api-Key: " \
 *   -d '{
 *     "lines": [
 *       {
 *         "materialCode": "SUT-001",
 *         "materialName": "Çiğ İnek Sütü",
 *         "quantity": 5000,
 *         "unit": "litre",
 *         "unitPrice": 28.50
 *       },
 *       {
 *         "materialCode": "MAYA-001",
 *         "materialName": "Peynir Mayası (Hayvansal)",
 *         "quantity": 20,
 *         "unit": "kg",
 *         "unitPrice": 850.00
 *       }
 *     ],
 *     "requestedDeliveryDate": "2025-06-10T08:00:00Z",
 *     "supplierId": "cccccccc-0001-0001-0001-000000000001",
 *     "notes": "Acil sipariş - bayram öncesi üretim"
 *   }'
 *
 * ── 2) WEB PORTAL (WebPortal) ────────────────────────────────
 *
 * curl -X POST https://api.mutevazipeynircilik.com/api/v1/orders \
 *   -H "Content-Type: application/json" \
 *   -H "X-Api-Key: " \
 *   -d '{
 *     "lines": [
 *       {
 *         "materialCode": "AMB-002",
 *         "materialName": "Karton Kutu (10lu Peynir)",
 *         "quantity": 3000,
 *         "unit": "adet",
 *         "unitPrice": 12.75
 *       },
 *       {
 *         "materialCode": "AMB-001",
 *         "materialName": "Vakumlu Peynir Poşeti (500g)",
 *         "quantity": 10000,
 *         "unit": "adet",
 *         "unitPrice": 1.20
 *       }
 *     ],
 *     "supplierId": "cccccccc-0001-0001-0001-000000000003",
 *     "notes": "Aylık rutin ambalaj siparişi"
 *   }'
 *
 * ── 3) ERP KONNEKTÖR (ERPKonnektör) ─────────────────────────
 *
 * curl -X POST https://api.mutevazipeynircilik.com/api/v1/orders \
 *   -H "Content-Type: application/json" \
 *   -H "X-Api-Key: mpk_erp_2025_ET843uhd8323e32" \
 *   -d '{
 *     "lines": [
 *       {
 *         "materialCode": "TUZ-001",
 *         "materialName": "Peynir Tuzu (İnce)",
 *         "quantity": 1500,
 *         "unit": "kg",
 *         "unitPrice": 8.90
 *       },
 *       {
 *         "materialCode": "KIM-001",
 *         "materialName": "Kalsiyum Klorür",
 *         "quantity": 50,
 *         "unit": "kg",
 *         "unitPrice": 145.00
 *       },
 *       {
 *         "materialCode": "MAYA-003",
 *         "materialName": "Starter Kültür (Mezofilik)",
 *         "quantity": 10,
 *         "unit": "kg",
 *         "unitPrice": 2200.00
 *       }
 *     ],
 *     "requestedDeliveryDate": "2025-06-15T06:00:00Z",
 *     "supplierId": "cccccccc-0001-0001-0001-000000000002",
 *     "notes": "Otomatik stok yenileme — ERP tetikledi"
 *   }'
 *
 * ── STOK SORGULAMA ──────────────────────────────────────────
 *
 * # Tüm stok
 * curl https://api.mutevazipeynircilik.com/api/v1/storage/items \
 *   -H "X-Api-Key: "
 *
 * # Depo özeti
 * curl https://api.mutevazipeynircilik.com/api/v1/storage/summary \
 *   -H "X-Api-Key: "
 *
 * # Boş alan
 * curl https://api.mutevazipeynircilik.com/api/v1/storage/available-space \
 *   -H "X-Api-Key: "
 *
 * # Süresi dolmak üzere olanlar (3 gün içinde)
 * curl "https://api.mutevazipeynircilik.com/api/v1/storage/expiring?daysAhead=3" \
 *   -H "X-Api-Key: "
 *
 * ╚══════════════════════════════════════════════════════════════╝
