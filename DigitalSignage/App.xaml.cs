using DigitalSignage.Models;
using Newtonsoft.Json;
using Serilog;
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

        if (!Directory.Exists("Download"))
            Directory.CreateDirectory("Download");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day)
            .CreateLogger();


        if (IsInternetAvailable()) return;

        var cacheFolder = Path.Combine(Directory.GetCurrentDirectory(), "Download");
        if (Directory.Exists(cacheFolder) && Directory.GetFiles(cacheFolder).Length > 0)
        {
            Log.Logger.Error("Нет интернета при запуске, но кэш доступен. Запускаем приложение с кэшем.");
        }
        else
        {
            MessageBox.Show("Нет подключения к интернету, и кэш отсутствует. Приложение будет закрыто.");
            Shutdown();
            return;
        }
    }

    private bool IsInternetAvailable()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = client.GetAsync("https://cloud-api.yandex.net").Result;
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ExplorerHelper.RunExplorer();
        base.OnExit(e);
    }
}