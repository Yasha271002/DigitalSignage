using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DigitalSignage.Models.Message;
using MainComponents.Popups;
using MvvmNavigationLib.Services;

namespace DigitalSignage.ViewModel;

public partial class PasswordPopupViewModel( string password) : ObservableObject
{
    [ObservableProperty] private bool _isPinPadOpen = true;

    private string _currentPassword = string.Empty;
    public string CurrentPassword
    {
        get => _currentPassword;
        set
        {
            SetProperty(ref _currentPassword, value);
            OnPropertyChanged(nameof(IsValid));
        }
    }

    public bool IsValid => CurrentPassword == password;

    
    [RelayCommand]
    private void Exit()=>Application.Current.Shutdown();

    [RelayCommand]
    private void RemoveSymbol()
    {
        if (CurrentPassword.Length > 0) CurrentPassword = CurrentPassword[..^1];
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private void AddSymbol(string symbol)
    {
        CurrentPassword += symbol;
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private void OpenPinPad()=>IsPinPadOpen = true;

    [RelayCommand]
    private void ClosePinPad()=>IsPinPadOpen = false;

    [RelayCommand]
    private void Close()
    {
        WeakReferenceMessenger.Default.Send(new QuitPopup());
    }
}