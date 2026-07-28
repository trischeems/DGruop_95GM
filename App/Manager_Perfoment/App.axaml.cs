using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using GM95.App.ManagerPerformance.Services;
using GM95.App.ManagerPerformance.ViewModels;
using GM95.App.ManagerPerformance.Views;

namespace GM95.App.ManagerPerformance;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Tranh trung validation giua CommunityToolkit va Avalonia.
            DisableAvaloniaDataAnnotationValidation();

            // Nap config app (config.json nguon + user-config.json ghi de) -> tao ApiClient -> VM shell.
            var config = AppConfig.Load();
            var api = new ApiClient(config);
            var vm = new MainWindowViewModel(api)
            {
                AppTitle = config.App.Vendor,
                AppSubtitle = "Quản lý sản xuất",
            };

            desktop.MainWindow = new MainWindow { DataContext = vm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var toRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToArray();
        foreach (var plugin in toRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
