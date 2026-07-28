namespace GM95.Server.Apps.ManagerPerformance.Contracts;

/// <summary>1 dong NVL trong phieu xuat (so luong xuat + don gia).</summary>
public sealed record MaterialIssueItemInput(
    long MaterialId,
    decimal QtyIssued,
    decimal UnitCost);

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
