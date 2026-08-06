namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Header dinh muc (BOM) tra ve client (khop cot bang bom).</summary>
public sealed record BomDto(
    long Id,
    long ProductId,
    int Version,
    string Status,
    DateTime? EffectiveFrom,
    string? Note);

/// <summary>1 dong NVL trong dinh muc (khop cot bom_items + ten/SKU NVL join san).</summary>
public sealed record BomItemDto(
    long Id,
    long BomId,
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials) - hien thi canh MaterialId
    string? MaterialName,    // Ten NVL (join materials)
    string? MaterialUomCode, // Ma DVT NVL (join units_of_measure) - hien thi canh so dinh muc
    string? MaterialUomName, // Ten DVT NVL (join units_of_measure)
    decimal QtyPerUnit,
    decimal WastePct,
    string? Note);

/// <summary>
/// 1 dong dinh muc PHANG (bom_items join header bom + products + materials + DVT).
/// Dung cho man hinh thong ke: lay TAT CA dong dinh muc bang 1 lan goi (tranh N+1).
/// </summary>
public sealed record BomItemRowDto(
    long BomId,
    int Version,             // version cua ban dinh muc (bom.version)
    string Status,           // trang thai ban dinh muc (bom.status)
    long ProductId,
    string? ProductSku,      // SKU ma hang (join products)
    string? ProductName,     // Ten ma hang (join products)
    string? ProductUomName,  // Ten DVT ma hang (join units_of_measure)
    long MaterialId,
    string? MaterialSku,     // SKU NVL (join materials)
    string? MaterialName,    // Ten NVL (join materials)
    string? MaterialUomName, // Ten DVT NVL (join units_of_measure)
    decimal QtyPerUnit,
    decimal WastePct,
    string? Note);

/// <summary>Chi tiet dinh muc: header + cac dong NVL.</summary>
public sealed record BomDetailDto(
    BomDto Bom,
    IEnumerable<BomItemDto> Items);

/// <summary>1 lan trinh duyet dinh muc (khop cot bang bom_approvals).</summary>
public sealed record BomApprovalDto(
    long Id,
    long BomId,
    string Status,
    long? RequestedBy,
    DateTime RequestedAt,
    long? DecidedBy,
    DateTime? DecidedAt,
    string? DecisionNote);

/// <summary>1 dong lich su thay doi dinh muc (khop cot bang bom_change_history).</summary>
public sealed record BomChangeHistoryDto(
    long Id,
    long BomId,
    long? BomItemId,
    long? MaterialId,
    string ChangeType,
    decimal? OldValue,
    decimal? NewValue,
    string? Reason,
    DateTime CreatedAt);

/// <summary>Yeu cau tao dinh muc moi (ban nhap DRAFT).</summary>
public sealed record CreateBomRequest(
    long ProductId,
    string? Note);

/// <summary>Yeu cau them/cap nhat 1 dong NVL trong dinh muc.</summary>
public sealed record UpsertBomItemRequest(
    long MaterialId,
    decimal QtyPerUnit,
    decimal WastePct,
    string? Note);

/// <summary>Yeu cau ra quyet dinh duyet/tu choi.</summary>
public sealed record DecisionRequest(
    long? DecidedBy,
    string? Note);

/// <summary>Anh huong khi xoa 1 BOM: trang thai, so dong dinh muc, so don dang dung.</summary>
public sealed record BomImpactDto(
    string Status,
    int Version,
    long ItemCount,
    long OrderCount,
    bool CanDelete);
