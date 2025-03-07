using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Models;
using DigitalSignage.Services;
using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using DigitalSignage.Models.Message;

namespace DigitalSignage.ViewModel;

public partial class MainWindowViewModel : ObservableObject, IRecipient<QuitPopup>
{
    private readonly IYandexDiskService _yandexService;
    private readonly ILogger _logger;
    private DispatcherTimer _timer;
    private int _currentIndex;

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

    private async Task UpdateMediaListAsync()
    {
        try
        {
            _logger.Information("Обновление списка медиа");
            var newItems = await _yandexService.GetMediaItemsAsync();
            if (!MediaItems.SequenceEqual(newItems))
            {
                MediaItems.Clear();
                foreach (var item in newItems.OrderBy(i => i.Order).ThenBy(i => i.OriginalFileName))
                {
                    MediaItems.Add(item);
                }
                _currentIndex = 0;
                _logger.Information("Список медиа обновлен, элементов: {Count}", MediaItems.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка обновления списка медиа");
        }
    }

    private async Task PlayAsync()
    {
        if (!MediaItems.Any()) return;
        PlayCurrentItem();
    }

    private void PlayCurrentItem()
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
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0
                };
                _timer.Interval = TimeSpan.FromSeconds(item.Duration);
                break;
            case FileType.Video:
                newContent = new MediaElement
                {
                    Source = new Uri(item.Path),
                    LoadedBehavior = MediaState.Play,
                    Stretch = Stretch.UniformToFill,
                    Opacity = 0
                };
                ((MediaElement)newContent).MediaOpened += (s, e) =>
                {
                    _logger.Information("Медиа {FileName} открыто, длительность: {Duration}",
                        item.OriginalFileName, ((MediaElement)newContent).NaturalDuration.HasTimeSpan ? ((MediaElement)newContent).NaturalDuration.TimeSpan.TotalSeconds : 0);
                };
                ((MediaElement)newContent).MediaEnded += async (s, e) => await NextItemAsync();
                ((MediaElement)newContent).MediaFailed += (s, e) =>
                {
                    _logger.Error("Ошибка воспроизведения медиа {FileName}: {Error}",
                        item.OriginalFileName, e.ErrorException?.Message);
                    _ = NextItemAsync();
                };
                break;
            case FileType.Audio:
                IsAudio = true;
                newContent = new MediaElement
                {
                    Source = new Uri(item.Path),
                    LoadedBehavior = MediaState.Play,
                    Stretch = Stretch.UniformToFill
                };
                ((MediaElement)newContent).MediaOpened += (s, e) =>
                {
                    _logger.Information("Медиа {FileName} открыто, длительность: {Duration}",
                        item.OriginalFileName, ((MediaElement)newContent).NaturalDuration.HasTimeSpan ? ((MediaElement)newContent).NaturalDuration.TimeSpan.TotalSeconds : 0);
                };
                ((MediaElement)newContent).MediaEnded += async (s, e) => await NextItemAsync();
                ((MediaElement)newContent).MediaFailed += (s, e) =>
                {
                    _logger.Error("Ошибка воспроизведения медиа {FileName}: {Error}",
                        item.OriginalFileName, e.ErrorException?.Message);
                    _ = NextItemAsync(); 
                };
                break;
            case FileType.Unknown:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (newContent == null) return;

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

    private async void Timer_Tick(object sender, EventArgs e) => await NextItemAsync();

    private async Task NextItemAsync()
    {
        if (CurrentContent != null)
        {
            
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(1) 
            };
            fadeOutAnimation.Completed += async (s, e) =>
            {
                _currentIndex++;
                if (_currentIndex >= MediaItems.Count)
                {
                    _currentIndex = 0;
                    await UpdateMediaListAsync();
                }
                PlayCurrentItem();
            };
            CurrentContent.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }
        else
        {
            _currentIndex++;
            if (_currentIndex >= MediaItems.Count)
            {
                _currentIndex = 0;
                await UpdateMediaListAsync();
            }
            PlayCurrentItem();
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

    private void Timer(object sender, EventArgs eventArgs)
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