using Refit;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using DigitalSignage.Models;
using Serilog;

namespace DigitalSignage.Services
{
    public class YandexDiskService : IYandexDiskService
    {
        private readonly IYandexDiskApi _api;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly AppConfig _config;
        private readonly string _cacheFolder = Path.Combine(Directory.GetCurrentDirectory(), "Download");

        public YandexDiskService(ILogger logger, AppConfig config)
        {
            _logger = logger;
            _config = config;
            _api = RestService.For<IYandexDiskApi>("https://cloud-api.yandex.net", new RefitSettings
            {
                ContentSerializer = new NewtonsoftJsonContentSerializer()
            });
            _httpClient = new HttpClient();
            Directory.CreateDirectory(_cacheFolder);
        }

        public async Task<ObservableCollection<MediaItem>> GetMediaItemsAsync()
        {
            var items = new ObservableCollection<MediaItem>();
            var internetAvailable = await IsInternetAvailableAsync();
            if (internetAvailable)
            {
                await FetchItemsFromFolderAsync(_config.FolderPath, items);
                await CleanUpCacheAsync(items); // Очистка кэша только при наличии интернета
            }
            else
            {
                _logger.Warning("Интернет недоступен, загрузка файлов из кэша без очистки");
                LoadFromCache(items); 
            }
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
                                    var fileType = GetFileType(extension);
                                    var duration = _config.DefaultImageDuration;
                                    var order = int.MaxValue;

                                    var parts = file.Name.Split('_');
                                    if (parts.Length >= 2 && int.TryParse(parts[0], out var parsedOrder))
                                    {
                                        order = parsedOrder;
                                        switch (fileType)
                                        {
                                            case FileType.Image when parts.Length == 2:
                                            {
                                                var durationStr = Path.GetFileNameWithoutExtension(file.Name).Split('_')[1];
                                                if (int.TryParse(durationStr, out var parsedDuration))
                                                {
                                                    duration = parsedDuration;
                                                }

                                                break;
                                            }
                                            case FileType.Image when parts.Length > 2:
                                            {
                                                var lastPart = parts[^1].Replace(extension, "");
                                                if (int.TryParse(lastPart, out var parsedLastDuration))
                                                {
                                                    duration = parsedLastDuration;
                                                }

                                                break;
                                            }
                                            case FileType.Unknown:
                                                break;
                                            case FileType.Video:
                                                break;
                                            case FileType.Audio:
                                                break;
                                            default:
                                                throw new ArgumentOutOfRangeException();
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
                var cachePath = Path.Combine(_cacheFolder, fileName);
                if (File.Exists(cachePath))
                {
                    _logger.Information("Файл {FileName} уже есть в кэше", fileName);
                    return cachePath;
                }

                _logger.Information("Скачивание файла {FileName}", fileName);
                var downloadLink = await _api.GetDownloadLink($"OAuth {_config.YandexDiskToken}", remotePath);
                var fileBytes = await _httpClient.GetByteArrayAsync(downloadLink.Href);
                await File.WriteAllBytesAsync(cachePath, fileBytes);
                _logger.Information("Файл {FileName} успешно скачан в кэш", fileName);
                return cachePath;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Ошибка скачивания файла {FileName}", fileName);
                throw;
            }
        }

        private async Task CleanUpCacheAsync(ObservableCollection<MediaItem> currentItems)
        {
            try
            {
                var cachedFiles = Directory.GetFiles(_cacheFolder);
                var currentFileNames = currentItems.Select(i => i.OriginalFileName).ToHashSet();

                foreach (var cachedFile in cachedFiles)
                {
                    var fileName = Path.GetFileName(cachedFile);
                    if (currentFileNames.Contains(fileName)) continue;
                    _logger.Information("Удаление устаревшего файла из кэша: {FileName}", fileName);
                    File.Delete(cachedFile);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Ошибка очистки кэша");
            }
        }

        private void LoadFromCache(ObservableCollection<MediaItem> items)
        {
            var cachedFiles = Directory.GetFiles(_cacheFolder);
            if (cachedFiles.Length == 0)
            {
                _logger.Warning("Кэш пуст, нет файлов для загрузки");
                return;
            }

            foreach (var file in cachedFiles)
            {
                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(fileName).ToLower();
                var fileType = GetFileType(extension);
                var duration = _config.DefaultImageDuration;
                var order = int.MaxValue;

                var parts = fileName.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[0], out var parsedOrder))
                {
                    order = parsedOrder;
                    switch (fileType)
                    {
                        case FileType.Image when parts.Length == 2:
                        {
                            var durationStr = Path.GetFileNameWithoutExtension(fileName).Split('_')[1];
                            if (int.TryParse(durationStr, out var parsedDuration))
                            {
                                duration = parsedDuration;
                            }

                            break;
                        }
                        case FileType.Image when parts.Length > 2:
                        {
                            var lastPart = parts[^1].Replace(extension, "");
                            if (int.TryParse(lastPart, out var parsedLastDuration))
                            {
                                duration = parsedLastDuration;
                            }

                            break;
                        }
                    }
                }

                items.Add(new MediaItem
                {
                    Order = order,
                    Path = file,
                    Duration = duration,
                    FileType = fileType,
                    OriginalFileName = fileName
                });
            }
            _logger.Information("Загружено {Count} элементов из кэша", items.Count);
        }

        private async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync("https://cloud-api.yandex.net");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
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
}