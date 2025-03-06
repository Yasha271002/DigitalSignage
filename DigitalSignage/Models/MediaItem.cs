using CommunityToolkit.Mvvm.ComponentModel;

namespace DigitalSignage.Models;

public partial class MediaItem : ObservableObject
{
    [ObservableProperty] private int _order;
    [ObservableProperty] private string _path = string.Empty;
    [ObservableProperty] private int _duration;
    [ObservableProperty] private FileType _fileType;
    [ObservableProperty] private string _originalFileName = string.Empty;
}