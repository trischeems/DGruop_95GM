using GM95.Server.Configuration;

namespace GM95.Server.Infrastructure.Web;

/// <summary>
/// Hop dong cho mot "app" (module) chay tren server dung chung.
/// Moi app = 1 folder rieng trong Server/Apps/&lt;Ten&gt;/ + 1 class implement IAppModule.
/// Them app moi (vd HRM): tao folder + class module + them 1 dong vao mang modules o Program.cs.
///
/// LUU Y ranh gioi: day la MODULE SERVER (API + DB) phuc vu app; KHAC voi app client
/// nam trong thu muc App/ (phan mem cai tren may nguoi dung).
/// </summary>
public interface IAppModule
{
    /// <summary>Ten module (vd "ManagerPerformance").</summary>
    string ModuleName { get; }

    /// <summary>Prefix route rieng cua app (vd "dgrpi"). App sau dung "dgrapp2"... KHONG dung "/api".</summary>
    string RoutePrefix { get; }

    /// <summary>Cac thu muc chua file V*.sql migration cua app (duong dan tuyet doi).</summary>
    IEnumerable<string> MigrationDirectories { get; }

    /// <summary>Dang ky DI cho repository/service cua module.</summary>
    void RegisterServices(IServiceCollection services, AppConfig config);
}
