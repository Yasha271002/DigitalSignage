using CommunityToolkit.Mvvm.ComponentModel;

namespace DigitalSignage.Models.Api;

public partial class DownloadLink : ObservableObject
{
    [ObservableProperty] private string _href = string.Empty;
}