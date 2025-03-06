using CommunityToolkit.Mvvm.ComponentModel;

namespace DigitalSignage.Models;

public partial class AppConfig : ObservableObject
{
    [ObservableProperty] private string _yandexDiskToken = string.Empty;
    [ObservableProperty] private string _folderPath = string.Empty;
    [ObservableProperty] private int _defaultImageDuration = 10;
    [ObservableProperty] private int _password;
}