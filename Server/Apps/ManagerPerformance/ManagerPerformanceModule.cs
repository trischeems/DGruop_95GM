using DGroup.Server.Apps.ManagerPerformance.Repositories;
using DGroup.Server.Apps.ManagerPerformance.Services;
using DGroup.Server.Configuration;
using DGroup.Server.Infrastructure.Web;

namespace DGroup.Server.Apps.ManagerPerformance;

/// <summary>
/// Module SERVER cho app Manager_Perfoment (quan ly san xuat may mac).
/// Route prefix: "dgrpi". DB: dung khoi dgroup_postgress (app dau tien).
/// LUU Y: day la phan server-side; app client (.NET UI) nam o thu muc App/ tach biet.
/// </summary>
public sealed class ManagerPerformanceModule : IAppModule
{
    public string ModuleName => "ManagerPerformance";

    // App dau tien dung prefix /dgrpi/. App sau: /dgrapp2/, /dgrapp3/...
    public string RoutePrefix => "dgrpi";

    public IEnumerable<string> MigrationDirectories => new[]
    {
        Path.Combine(AppContext.BaseDirectory, "Apps", "ManagerPerformance", "Database", "Migrations"),
    };

    public void RegisterServices(IServiceCollection services, AppConfig config)
    {
        // Repository (Dapper raw SQL) — scoped theo request.
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<ILookupRepository, LookupRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IBomRepository, BomRepository>();
        services.AddScoped<IProductionOrderRepository, ProductionOrderRepository>();
        services.AddScoped<IProductionPlanRepository, ProductionPlanRepository>();
        services.AddScoped<IProductionStepRepository, ProductionStepRepository>();
        services.AddScoped<IMaterialIssueRepository, MaterialIssueRepository>();
        services.AddScoped<IFinishedGoodsRepository, FinishedGoodsRepository>();
        services.AddScoped<ILossReportRepository, LossReportRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();

        // Service (nghiep vu + dieu phoi transaction).
        services.AddScoped<MaterialService>();
        services.AddScoped<StockService>();
        services.AddScoped<ReportService>();
        services.AddScoped<LookupService>();
        services.AddScoped<ProductService>();
        services.AddScoped<BomService>();
        services.AddScoped<ProductionOrderService>();
        services.AddScoped<ProductionPlanService>();
        services.AddScoped<ProductionStepService>();
        services.AddScoped<MaterialIssueService>();
        services.AddScoped<FinishedGoodsService>();
        services.AddScoped<LossReportService>();
        services.AddScoped<AlertService>();
    }
}
