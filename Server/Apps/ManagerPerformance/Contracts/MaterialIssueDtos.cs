namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>1 dong NVL trong phieu xuat (so luong xuat + don gia + cap cho mat hang nao).</summary>
public sealed record MaterialIssueItemInput(
    long MaterialId,
    decimal QtyIssued,
    decimal UnitCost,
    long? ProductionOrderItemId = null); // Cap NVL cho MAT HANG nao trong don (V007; null = don 1 mat hang)

/// <summary>Yeu cau tao phieu xuat NVL cho san xuat (tru ton kho).</summary>
public sealed record CreateMaterialIssueRequest(
    long ProductionOrderId,
    long WarehouseId,
    IEnumerable<MaterialIssueItemInput> Items,
    string? Note);

/// <summary>Ket qua sau khi tao & POST phieu xuat.</summary>
public sealed record MaterialIssueResultDto(
    long IssueId,
    string IssueNo,
    int LineCount);

/// <summary>Phieu xuat NVL (dong tom tat, khop bang material_issues).</summary>
public sealed record MaterialIssueDto(
    long Id,
    string IssueNo,
    long ProductionOrderId,
    long WarehouseId,
    string Status,
    DateTime IssuedAt);

/// <summary>1 dong NVL cua phieu xuat + thong tin phieu/NVL join san (hien thi bang co ma + ten NVL).</summary>
public sealed record MaterialIssueItemDto(
    long Id,
    long MaterialIssueId,
    string IssueNo,
    long ProductionOrderId,
    long MaterialId,
    string? MaterialSku,     // ma NVL (join materials)
    string? MaterialName,    // ten NVL (join materials)
    string? MaterialUomCode, // ma DVT - hien canh so luong
    string? MaterialUomName, // ten DVT
    decimal QtyIssued,
    decimal UnitCost,
    long WarehouseId,
    string? WarehouseName,   // ten kho (join warehouses)
    string Status,
    DateTime IssuedAt);
