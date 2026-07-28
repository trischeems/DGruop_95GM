namespace GM95.Server.Apps.ManagerPerformance.Contracts;

// =====================================================================================
// QUY TRINH SAN XUAT LINH HOAT (V006)
//   - production_stages  : DANH MUC cong doan (Cat, May, QC, In, Theu, Dong goi...).
//   - production_routings: MAU quy trinh = 1 chuoi cong doan (vd "Quy trinh ao thun").
//   - routing_steps      : cac buoc trong 1 mau (cong doan + thu tu + co bo qua duoc khong).
//   - don chon 1 mau -> sinh buoc; sau do van SUA RIENG duoc tren tung don.
// =====================================================================================

/// <summary>1 cong doan trong danh muc (dung chung cho moi mau quy trinh).</summary>
public sealed record StageDto(
    long Id,
    string Code,
    string Name,
    int Seq,             // thu tu goi y khi hien danh muc
    bool IsActive);

public sealed record CreateStageRequest(string Code, string Name, int? Seq);
public sealed record UpdateStageRequest(string Name, int? Seq, bool IsActive);

/// <summary>1 mau quy trinh (kem so buoc de hien danh sach cho nhanh).</summary>
public sealed record RoutingDto(
    long Id,
    string Code,
    string Name,
    string? Note,
    bool IsActive,
    bool IsDefault,
    int StepCount,       // so buoc trong mau
    DateTime CreatedAt);

/// <summary>1 buoc trong mau quy trinh.</summary>
public sealed record RoutingStepDto(
    long Id,
    long RoutingId,
    long StageId,
    string StageCode,
    string StageName,
    int Seq,
    bool IsOptional,     // buoc nay duoc phep bo qua khi chay don
    string? Note);

/// <summary>Mau quy trinh + day du cac buoc (dung khi xem/sua 1 mau).</summary>
public sealed record RoutingDetailDto(
    RoutingDto Routing,
    IEnumerable<RoutingStepDto> Steps);

/// <summary>1 dong buoc khi tao/sua mau (client gui len ca chuoi).</summary>
public sealed record RoutingStepInput(
    long StageId,
    int Seq,
    bool IsOptional,
    string? Note);

public sealed record CreateRoutingRequest(
    string Code,
    string Name,
    string? Note,
    bool IsDefault,
    IEnumerable<RoutingStepInput> Steps);

public sealed record UpdateRoutingRequest(
    string Name,
    string? Note,
    bool IsActive,
    bool IsDefault,
    IEnumerable<RoutingStepInput> Steps);

// ------------------------------------------------------------------------------------
// SUA BUOC RIENG CHO TUNG DON
// ------------------------------------------------------------------------------------

/// <summary>Ap 1 mau quy trinh vao don: sinh lai cac buoc theo mau.</summary>
/// <param name="RoutingId">Mau quy trinh chon.</param>
/// <param name="StartSeq">Bat dau tu buoc thu may cua mau (bo qua cac buoc truoc do). Null = tu dau.</param>
/// <param name="Replace">true = xoa cac buoc CHUA LAM roi sinh lai; false = chi them buoc con thieu.</param>
public sealed record ApplyRoutingRequest(
    long RoutingId,
    int? StartSeq,
    bool Replace);

/// <summary>Them 1 cong doan le vao don (khong co trong mau).</summary>
public sealed record AddOrderStepRequest(long StageId, int? Seq, string? Note);

/// <summary>Bat/tat co BO QUA cho 1 buoc cua don.</summary>
public sealed record SkipStepRequest(bool IsSkipped, string? Note);

/// <summary>Doi thu tu cac buoc cua don (client gui ca danh sach id theo thu tu moi).</summary>
public sealed record ReorderStepsRequest(IEnumerable<long> StepIdsInOrder);
