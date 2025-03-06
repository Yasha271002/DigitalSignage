using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace DigitalSignage.Models.Api;

public partial class YandexFile : ObservableObject
{
    [ObservableProperty, JsonProperty("name")] private string _name = string.Empty;
    [ObservableProperty, JsonProperty("path")] private string _path = string.Empty;
    [ObservableProperty, JsonProperty("type")] private string _type = string.Empty;
}