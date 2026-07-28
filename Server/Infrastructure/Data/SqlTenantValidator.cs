using System.Text.RegularExpressions;

namespace GM95.Server.Infrastructure.Data;

/// <summary>
/// Kiem tra ten tenant (= ten PostgreSQL schema) truoc khi ghep vao SQL.
/// search_path KHONG nhan tham so bind ($1) nen phai tu bao ve: 2 lop
///   1) regex ky tu hop le
///   2) phai nam trong whitelist (allowed_tenants tu config)
/// Ket hop QuoteIdentifier khi ghep SQL (o TenantConnection) -> chong SQL injection.
/// </summary>
public static class SqlTenantValidator
{
    // Bat dau bang chu thuong hoac '_', theo sau la chu thuong/so/'_', toi da 63 ky tu (gioi han PostgreSQL).
    private static readonly Regex Valid = new(
        "^[a-z_][a-z0-9_]{0,62}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Chuan hoa + kiem tra. Throw neu khong hop le. Tra ve ten da chuan hoa (lowercase).</summary>
    public static string Validate(string? tenant, IReadOnlySet<string> allowed)
    {
        var t = (tenant ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(t))
            throw new ArgumentException("Ten tenant rong.");

        if (!Valid.IsMatch(t))
            throw new ArgumentException(
                $"Ten tenant khong hop le: '{tenant}'. Chi cho phep chu thuong, so, '_', dai <= 63.");

        if (!allowed.Contains(t))
            throw new ArgumentException(
                $"Tenant '{t}' khong nam trong danh sach cho phep (allowed_tenants).");

        return t;
    }

    /// <summary>Kiem tra dinh dang (khong doi chieu whitelist) — dung cho tao schema moi.</summary>
    public static bool IsWellFormed(string? tenant) =>
        !string.IsNullOrEmpty(tenant) && Valid.IsMatch(tenant);
}
