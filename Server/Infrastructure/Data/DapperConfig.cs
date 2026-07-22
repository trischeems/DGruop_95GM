using Dapper;

namespace DGroup.Server.Infrastructure.Data;

/// <summary>
/// Cau hinh Dapper toan cuc (goi 1 lan luc khoi dong).
/// Bat khop ten cot snake_case (PostgreSQL) voi property PascalCase (C#).
/// </summary>
public static class DapperConfig
{
    private static bool _configured;

    public static void Configure()
    {
        if (_configured) return;
        // vi_du: cot "created_at" -> property "CreatedAt"
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        _configured = true;
    }
}
