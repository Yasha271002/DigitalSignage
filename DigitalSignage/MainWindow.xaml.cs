using Serilog;
using System.Windows;
using DigitalSignage.ViewModel;

namespace DigitalSignage;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        InitializeComponent();
        if (App.Config != null)
        {
            DataContext = new MainWindowViewModel(
                new Services.YandexDiskService(Log.Logger, App.Config),
                Log.Logger);
        }
    }
}