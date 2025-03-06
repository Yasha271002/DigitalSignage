using DigitalSignage.Models.Api;
using Refit;

namespace DigitalSignage.Services;

public interface IYandexDiskApi
{
    [Get("/v1/disk/resources?path={path}")]
    Task<YandexDiskResponse> GetFiles([Header("Authorization")] string auth, string path);

    [Get("/v1/disk/resources/download?path={path}")]
    Task<DownloadLink> GetDownloadLink([Header("Authorization")] string auth, string path);
}