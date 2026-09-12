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
            vm.OpenFilePickerAsync = async () =>
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
            };

            vm.SaveFilePickerAsync = async () =>
            {
                var topLevel = GetTopLevel(this);
                if (topLevel == null) return null;

                return await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Plist File",
                    DefaultExtension = "plist",
                    FileTypeChoices = [new FilePickerFileType("Plist Files") { Patterns = ["*.plist"] }]
                });
            };
        }
    }
}