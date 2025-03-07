using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Models;
using DigitalSignage.Services;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using DigitalSignage.Models.Message;

namespace DigitalSignage.ViewModel;

public partial class MainWindowViewModel : ObservableObject, IRecipient<QuitPopup>
{
    private readonly IYandexDiskService _yandexService;
    private readonly ILogger _logger;
    private DispatcherTimer _timer;
    private int _currentIndex;
    private bool _isUpdating;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private PasswordPopupViewModel _passwordPopupViewModel = new(App.Config.Password.ToString());
    private readonly DispatcherTimer _popupTimer = new();
    private int _sec;

    [ObservableProperty]
    private UIElement currentContent;

    public ObservableCollection<MediaItem> MediaItems { get; } = [];

    [ObservableProperty] private bool _isAudio;

    [RelayCommand]
    private async Task Play() => await PlayAsync();

    public MainWindowViewModel(IYandexDiskService yandexService, ILogger logger)
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
        _yandexService = yandexService;
        _logger = logger;
        _timer = new DispatcherTimer();
        _timer.Tick += Timer_Tick;
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        await UpdateMediaListAsync();
        if (MediaItems.Any()) await PlayAsync();
    }

    private async Task UpdateMediaListAsync(bool backgroundUpdate = false)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        try
        {
            _logger.Information("Обновление списка медиа, фоновое: {Background}", backgroundUpdate);
            var newItems = await _yandexService.GetMediaItemsAsync();
            if (!MediaItems.SequenceEqual(newItems))
            {
                var currentItem = _currentIndex < MediaItems.Count ? MediaItems[_currentIndex] : null;
                MediaItems.Clear();
                foreach (var item in newItems.OrderBy(i => i.Order).ThenBy(i => i.OriginalFileName))
                {
                    MediaItems.Add(item);
                }

                switch (backgroundUpdate)
                {
                    case true when currentItem != null:
                    {
                        _currentIndex = MediaItems.IndexOf(MediaItems.FirstOrDefault(i => i.OriginalFileName == currentItem.OriginalFileName));
                        if (_currentIndex < 0) _currentIndex = 0;
                        break;
                    }
                    case false:
                        _currentIndex = 0;
                        break;
                }
                _logger.Information("Список медиа обновлен, элементов: {Count}", MediaItems.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка обновления списка медиа");
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private async Task PlayAsync()
    {
        if (!MediaItems.Any()) return;
        await PlayCurrentItemWithAnimation();
    }

    private async Task PlayCurrentItemWithAnimation()
    {
        IsAudio = false;
        var item = MediaItems[_currentIndex];
        _timer.Stop();
        _logger.Information("Воспроизведение элемента {Index}: {Path}", _currentIndex, item.Path);

        UIElement newContent = null;
        switch (item.FileType)
        {
            case FileType.Image:
                newContent = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(item.Path)),
                    Stretch = Stretch.Uniform,
                    Opacity = 0
                };
                _timer.Interval = TimeSpan.FromSeconds(item.Duration);
                break;
            case FileType.Video:
                newContent = new MediaElement
                {
                    Source = new Uri(item.Path),
                    LoadedBehavior = MediaState.Play,
                    Stretch = Stretch.Uniform,
                    Opacity = 0
                };
                SetupMediaElement((MediaElement)newContent, item);
                break;
            case FileType.Audio:
                IsAudio = true;
                var imagePath = Path.Combine(Path.GetDirectoryName(item.Path),
                    Path.GetFileNameWithoutExtension(item.OriginalFileName) + ".png");
                var finalImagePath = File.Exists(imagePath) ? imagePath : Path.Combine(Directory.GetCurrentDirectory(), App.Config.DefaultAudioImage);

                if (!File.Exists(finalImagePath))
                {
                    _logger.Warning("Стандартное изображение {DefaultImage} не найдено", App.Config.DefaultAudioImage);
                    finalImagePath = null;
                }

                if (finalImagePath != null)
                {
                    newContent = new Grid
                    {
                        Opacity = 0,
                        Children =
                        {
                            new Image
                            {
                                Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(finalImagePath)),
                                Stretch = Stretch.Uniform
                            },
                            new MediaElement
                            {
                                Source = new Uri(item.Path),
                                LoadedBehavior = MediaState.Play
                            }
                        }
                    };
                    var mediaElement = (MediaElement)((Grid)newContent).Children[1];
                    SetupMediaElement(mediaElement, item);
                    mediaElement.MediaEnded -= async (s, e) => await NextItemAsync();
                    mediaElement.MediaEnded += async (s, e) =>
                    {
                        var fadeOutAnimation = new DoubleAnimation
                        {
                            From = 1,
                            To = 0,
                            Duration = TimeSpan.FromSeconds(0)
                        };
                        fadeOutAnimation.Completed += async (_, __) =>
                        {
                            CurrentContent = null;
                            await PlayNextItemWithAnimation();
                        };
                        newContent.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                    };
                }
                else
                {
                    newContent = new MediaElement
                    {
                        Source = new Uri(item.Path),
                        LoadedBehavior = MediaState.Play,
                        Stretch = Stretch.Uniform,
                        Opacity = 0
                    };
                    SetupMediaElement((MediaElement)newContent, item);
                }
                break;
            case FileType.Unknown:
                await NextItemAsync();
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (newContent != null)
        {
            CurrentContent = newContent;
            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(1)
            };
            newContent.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);

            if (item.FileType == FileType.Image)
            {
                _timer.Start();
            }
        }
    }

    private async Task PlayNextItemWithAnimation()
    {
        _currentIndex++;
        if (_currentIndex >= MediaItems.Count)
        {
            _currentIndex = 0;
            _ = Task.Run(() => UpdateMediaListAsync(true));
        }
        await PlayCurrentItemWithAnimation();
    }

    private void SetupMediaElement(MediaElement media, MediaItem item)
    {
        media.MediaOpened += (s, e) =>
        {
            _logger.Information("Медиа {FileName} открыто, длительность: {Duration}",
                item.OriginalFileName, media.NaturalDuration.HasTimeSpan ? media.NaturalDuration.TimeSpan.TotalSeconds : 0);
        };
        media.MediaEnded += async (s, e) => await NextItemAsync();
        media.MediaFailed += (s, e) =>
        {
            _logger.Error("Ошибка воспроизведения медиа {FileName}: {Error}",
                item.OriginalFileName, e.ErrorException?.Message);
            _ = NextItemAsync();
        };
    }

    private async void Timer_Tick(object sender, EventArgs e) => await NextItemAsync();

    private async Task NextItemAsync()
    {
        if (CurrentContent != null && !IsAudio)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(1)
            };
            fadeOutAnimation.Completed += async (s, e) =>
            {
                CurrentContent = null;
                await PlayNextItemWithAnimation();
            };
            CurrentContent.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }
        else
        {
            await PlayNextItemWithAnimation();
        }
    }

    [RelayCommand]
    private void StopTimer()
    {
        _popupTimer.Tick -= Timer;
        _popupTimer.Stop();
        _sec = 0;
    }

    [RelayCommand]
    private void StartTimer()
    {
        _popupTimer.Stop();
        _sec = 0;
        _popupTimer.Interval = TimeSpan.FromSeconds(1);
        _popupTimer.Tick += Timer;
        _popupTimer.Start();
    }

    private void Timer(object sender, EventArgs e)
    {
        _sec++;
        if (_sec < 7) return;
        IsOpen = true;
    }

    public void Receive(QuitPopup message)
    {
        IsOpen = false;
    }
}