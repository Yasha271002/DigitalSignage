using DigitalSignage.Models;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using DigitalSignage.Helpers;

namespace DigitalSignage;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static AppConfig? Config { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var configText = File.ReadAllText("config.json");
            Config = JsonConvert.DeserializeObject<AppConfig>(configText);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка загрузки конфигурации: {ex.Message}");
            Shutdown();
            return;
        }

        ExplorerHelper.KillExplorer();

        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ExplorerHelper.RunExplorer();
        base.OnExit(e);
    }
}