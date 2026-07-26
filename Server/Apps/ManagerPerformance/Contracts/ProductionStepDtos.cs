namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>1 buoc quy trinh san xuat (Cat/May/QC/Nhap TP) cua 1 don, kem thong tin cong doan.</summary>
public sealed record ProductionStepDto(
    long Id,
    long ProductionOrderId,
    long StageId,
    string StageCode,
    string StageName,
    int Seq,
    string Status,
    decimal QtyIn,
    decimal QtyOut,
    decimal QtyDefect,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string? Note,
    bool IsSkipped,          // true = buoc nay duoc BO QUA cho don (nhay buoc, khong phai lam)
    string? ProductUomCode,  // Ma DVT thanh pham (join units_of_measure qua production_orders.product_id)
    string? ProductUomName); // Ten DVT thanh pham (join units_of_measure qua production_orders.product_id)

/// <summary>Cap nhat 1 buoc quy trinh: trang thai + so luong vao/ra/loi + ghi chu.</summary>
public sealed record UpdateStepRequest(
    string Status,
    decimal QtyIn,
    decimal QtyOut,
    decimal QtyDefect,
    string? Note);
