using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PlistEditor.Services;
using PlistEditor.ViewModels;
using System;
using System.Threading.Tasks;

namespace PlistEditor.Views;

public partial class MainWindow : Window
{
    private double _lastValidWidth = 1280;
    private double _lastValidHeight = 720;

    public MainWindow()
    {
        // 1. Restore dimensions BEFORE the window is rendered on screen
        RestoreWindowSettings();

        InitializeComponent();

        // 2. Track size changes & handle window close
        Closing += OnWindowClosing;
        SizeChanged += OnWindowSizeChanged;

        // 3. Set up the ViewModel with file operations
        DataContextChanged += OnDataContextChanged;
    }

    private void RestoreWindowSettings()
    {
        var settings = SettingsService.Current;

        if (settings.WindowWidth >= 400)
        {
            Width = settings.WindowWidth;
            _lastValidWidth = settings.WindowWidth;
        }

        if (settings.WindowHeight >= 300)
        {
            Height = settings.WindowHeight;
            _lastValidHeight = settings.WindowHeight;
        }

        if (Enum.TryParse<WindowState>(settings.WindowState, out var state))
        {
            WindowState = state;
        }
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Capture active dimensions, ignoring teardown collapse
        if (WindowState == WindowState.Normal && e.NewSize.Width >= 400 && e.NewSize.Height >= 300)
        {
            _lastValidWidth = e.NewSize.Width;
            _lastValidHeight = e.NewSize.Height;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var settings = SettingsService.Current;
        settings.WindowState = WindowState.ToString();
        settings.WindowWidth = _lastValidWidth;
        settings.WindowHeight = _lastValidHeight;

        SettingsService.SaveSettings();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenFilePickerAsync = OpenFileAsync;
            vm.SaveFilePickerAsync = SaveFileAsync;
            vm.OpenFolderPickerAsync = OpenFolderAsync;
            vm.GetTopLevelWindow = () => this;
        }
    }

    private async Task<IStorageFolder?> OpenFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select OC / EFI Folder for Snapshot",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0] : null;
    }

    private async Task<IStorageFile?> SaveFileAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

        return await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Plist File",
            DefaultExtension = "plist",
            FileTypeChoices = [new FilePickerFileType("Plist Files") { Patterns = ["*.plist"] }]
        });
    }

    private async Task<IStorageFile?> OpenFileAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open OpenCore Plist File",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Plist Files") { Patterns = ["*.plist"] }, FilePickerFileTypes.All]
        });

        return files.Count > 0 ? files[0] : null;
    }
}