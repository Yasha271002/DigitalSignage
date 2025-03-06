using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace DigitalSignage.Models.Api;

public class YandexDiskResponse
{
    [ JsonProperty("_embedded")] public Embedded Embedded { get; set; }
}

public class Embedded 
{
    [JsonProperty("items")] public YandexFile[] Items { get; set; }
}