namespace DGroup.Server.Apps.ManagerPerformance.Contracts;

/// <summary>Canh bao ton thap / don thieu NVL (khop bang alerts).</summary>
public sealed record AlertDto(
    long Id,
    string AlertType,
    string Severity,
    string EntityType,
    long EntityId,
    string Message,
    string Status,
    DateTime CreatedAt);
