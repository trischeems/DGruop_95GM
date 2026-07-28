using GM95.Server.Configuration;

namespace GM95.Server.Infrastructure.Web;

/// <summary>
/// Danh sach cac app module da dang ky. Them app moi = them 1 dong o day.
/// (Dang ky tuong minh, khong reflection -> ro rang, de kiem soat.)
/// </summary>
public static class ModuleRegistry
{
    public static IReadOnlyList<IAppModule> BuildModules() => new IAppModule[]
    {
        new Apps.ManagerPerformance.ManagerPerformanceModule(),
        // new Apps.Hrm.HrmModule(),   // <- vi du them app HRM sau nay
    };

    public static void RegisterAll(
        IReadOnlyList<IAppModule> modules, IServiceCollection services, AppConfig config)
    {
        foreach (var m in modules)
            m.RegisterServices(services, config);
    }
}
