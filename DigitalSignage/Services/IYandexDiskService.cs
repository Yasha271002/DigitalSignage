using System.Collections.ObjectModel;
using DigitalSignage.Models;

namespace DigitalSignage.Services;

public interface IYandexDiskService
{
    Task<ObservableCollection<MediaItem>> GetMediaItemsAsync();
    Task<string> DownloadFileAsync(string remotePath, string fileName);
}