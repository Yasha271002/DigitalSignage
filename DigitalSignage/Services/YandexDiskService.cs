using Refit;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DigitalSignage.Models;
using Serilog;

namespace DigitalSignage.Services;

public class YandexDiskService : IYandexDiskService
{
    private readonly IYandexDiskApi _api;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly AppConfig _config;

    public YandexDiskService(ILogger logger, AppConfig config)
    {
        _logger = logger;
        _config = config;
        _api = RestService.For<IYandexDiskApi>("https://cloud-api.yandex.net", new RefitSettings
        {
            ContentSerializer = new NewtonsoftJsonContentSerializer()
        });
        _httpClient = new HttpClient();
    }

    public async Task<ObservableCollection<MediaItem>> GetMediaItemsAsync()
    {
        var items = new ObservableCollection<MediaItem>();
        await FetchItemsFromFolderAsync(_config.FolderPath, items);
        return items;
    }

    private async Task FetchItemsFromFolderAsync(string folderPath, ObservableCollection<MediaItem> items)
    {
        try
        {
            _logger.Information("Получение списка файлов из {Folder}", folderPath);
            var response = await _api.GetFiles($"OAuth {_config.YandexDiskToken}", folderPath);

            if (response.Embedded?.Items != null)
            {
                foreach (var file in response.Embedded.Items)
                {
                    try
                    {
                        switch (file.Type)
                        {
                            case "file":
                            {
                                var extension = Path.GetExtension(file.Name).ToLower();
                                var duration = _config.DefaultImageDuration; 
                                var order = int.MaxValue;

                                var firstUnderscoreIndex = file.Name.IndexOf('_');
                                if (firstUnderscoreIndex > 0)
                                {
                                    var orderStr = file.Name[..firstUnderscoreIndex];
                                    if (int.TryParse(orderStr, out var parsedOrder))
                                    {
                                        order = parsedOrder;
                                    }
                                }

                                var fileType = GetFileType(extension);
                                if (fileType == FileType.Image)
                                {
                                    var lastUnderscoreIndex = file.Name.LastIndexOf('_');
                                    if (lastUnderscoreIndex > firstUnderscoreIndex &&
                                        lastUnderscoreIndex < file.Name.Length - extension.Length - 1)
                                    {
                                        var durationStr = file.Name.Substring(lastUnderscoreIndex + 1,
                                            file.Name.Length - lastUnderscoreIndex - 1 - extension.Length);
                                        int.TryParse(durationStr, out duration);
                                    }
                                }

                                var localPath = await DownloadFileAsync(file.Path, file.Name);
                                items.Add(new MediaItem
                                {
                                    Order = order,
                                    Path = localPath,
                                    Duration = duration,
                                    FileType = fileType,
                                    OriginalFileName = file.Name
                                });
                                break;
                            }
                            case "dir":
                                await FetchItemsFromFolderAsync(file.Path, items);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Ошибка обработки элемента {Name}", file.Name);
                    }
                }
            }
            _logger.Information("Успешно обработано {Count} элементов в папке {Folder}", items.Count, folderPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка получения списка файлов из {Folder}", folderPath);
        }
    }

    public async Task<string> DownloadFileAsync(string remotePath, string fileName)
    {
        try
        {
            _logger.Information("Скачивание файла {FileName}", fileName);
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);
            var downloadLink = await _api.GetDownloadLink($"OAuth {_config.YandexDiskToken}", remotePath);
            var fileBytes = await _httpClient.GetByteArrayAsync(downloadLink.Href);
            await File.WriteAllBytesAsync(tempPath, fileBytes);
            _logger.Information("Файл {FileName} успешно скачан", fileName);
            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка скачивания файла {FileName}", fileName);
            throw;
        }
    }

    private FileType GetFileType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" or ".png" or ".tiff" or ".gif" => FileType.Image,
        ".avi" or ".mp4" or ".m4v" or ".mkv" or ".mov" or ".mpeg" or ".wmv" => FileType.Video,
        ".mp3" or ".wav" => FileType.Audio,
        _ => FileType.Unknown
    };
}