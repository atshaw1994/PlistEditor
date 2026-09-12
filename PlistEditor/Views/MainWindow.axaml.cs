using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PlistEditor.ViewModels;

namespace PlistEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenFilePickerAsync = OpenFileAsync;

            vm.SaveFilePickerAsync = SaveFileAsync;

            vm.OpenFolderPickerAsync = OpenFolderAsync;
        }
    }

    private async System.Threading.Tasks.Task<IStorageFolder?> OpenFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select OC / EFI Folder for Snapshot",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0] : null;
    }

    private async System.Threading.Tasks.Task<IStorageFile?> SaveFileAsync()
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

    private async System.Threading.Tasks.Task<IStorageFile?> OpenFileAsync()
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