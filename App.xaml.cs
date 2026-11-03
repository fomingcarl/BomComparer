// Location: App.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using BomComparer.Services;
using BomComparer.ViewModels;

namespace BomComparer
{
    public partial class App : Application
    {
        public static ServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var serviceCollection = new ServiceCollection();

            // Register Services
            serviceCollection.AddSingleton<IExcelService, ExcelService>();
            serviceCollection.AddSingleton<IComparisonService, ComparisonService>();

            // Register ViewModels and Windows
            serviceCollection.AddTransient<MainViewModel>();
            serviceCollection.AddTransient<MainWindow>();

            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Set and show the main window
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}