using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlistEditor.Helpers;
using PlistEditor.Models;
using PlistEditor.Services;
using PlistEditor.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PlistEditor.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public string WindowTitle => $"PlistEditor - {System.IO.Path.GetFileName(PlistFilePath)}{(IsDirty ? " *" : "")}";
    public bool HasLoadedPlist => !string.IsNullOrEmpty(PlistFilePath) && PlistFilePath != "No file opened";
    public ObservableCollection<PlistSectionViewModel> FirstLevel { get; } = [];

    public Func<Task<IStorageFile?>>? OpenFilePickerAsync { get; set; }
    public Func<Task<IStorageFolder?>>? OpenFolderPickerAsync { get; set; }
    public Func<Task<IStorageFile?>>? SaveFilePickerAsync { get; set; }
    public Func<Window?>? GetTopLevelWindow { get; set; }

    [ObservableProperty] public partial PlistSectionViewModel? SelectedSection { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    public partial bool IsDirty { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(HasLoadedPlist))]
    public partial string PlistFilePath { get; set; } = "No file opened";

    [ObservableProperty] public partial SmbiosViewModel Smbios { get; set; } = new();

    [RelayCommand]
    public async Task OpenPlistAsync()
    {
        if (OpenFilePickerAsync == null) return;

        // Open native cross-platform file picker
        var file = await OpenFilePickerAsync();
        if (file == null) return;

        try
        {
            PlistFilePath = file.Path.LocalPath;
            await using var stream = await file.OpenReadAsync();
            XDocument doc = await XDocument.LoadAsync(stream, LoadOptions.None, default);

            XElement? rootDict = doc.Root?.Element("dict");
            if (rootDict == null) return;

            FirstLevel.Clear();

            var elements = rootDict.Elements().ToList();
            for (int i = 0; i < elements.Count - 1; i++)
            {
                if (elements[i].Name.LocalName == "key")
                {
                    string keyName = elements[i].Value;
                    XElement valueElement = elements[i + 1];

                    if (!keyName.StartsWith('#'))
                    {
                        FirstLevel.Add(PlistSectionViewModel.Create(keyName, valueElement, FirstLevel));
                    }
                }
            }

            SelectedSection = FirstLevel.FirstOrDefault();

            // After loading root sections:
            foreach (var section in FirstLevel)
            {
                section.AttachChangeTracker(MarkDirty);
            }

            IsDirty = false; // Reset clean state on newly loaded file
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing plist: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SavePlistAsync()
    {
        if (SaveFilePickerAsync == null) return;

        var file = await SaveFilePickerAsync();
        if (file == null) return;

        try
        {
            // Reconstruct XML from ViewModel hierarchy
            XElement rootDict = new("dict");
            foreach (var section in FirstLevel)
            {
                rootDict.Add(new XElement("key", section.Name));
                rootDict.Add(section.ToXElement());
            }

            XDocument doc = new(
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), rootDict)
            );

            await using var stream = await file.OpenWriteAsync();
            await doc.SaveAsync(stream, SaveOptions.None, default);

            IsDirty = false; // Mark clean after saving
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving plist: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SnapshotAsync() => await PerformSnapshotAsync(isClean: false);

    [RelayCommand]
    public async Task CleanSnapshotAsync() => await PerformSnapshotAsync(isClean: true);

    [RelayCommand]
    public async Task OpenSmbiosGeneratorAsync()
    {
        if (GetTopLevelWindow == null) return;
        var owner = GetTopLevelWindow();
        if (owner == null) return;

        var dialog = new SmbiosGenerator(this);
        var result = await dialog.ShowDialog<SmbiosModel?>(owner);

        if (result != null)
        {
            Smbios.PopulateFromModel(result);
            SmbiosService.InjectToPlatformInfo(this, result);
            MarkDirty();
        }
    }

    public MainViewModel()
    {
        // Check if Avalonia is running in the Visual Studio XAML Designer
        if (Design.IsDesignMode)
        {
            LoadEmbeddedSample();
            PlistFilePath = "Sample.plist (Design Mode)";
        }
    }

    private void LoadEmbeddedSample()
    {
        try
        {
            var uri = new Uri("avares://PlistEditor/Assets/Sample.plist");

            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                XDocument doc = XDocument.Load(stream);
                ParsePlistDocument(doc);
            }
        }
        catch (Exception ex)
        {
            // Log locally so previewer process doesn't fail silently
            System.Diagnostics.Debug.WriteLine($"Design mode error: {ex.Message}");
        }
    }

    private async Task PerformSnapshotAsync(bool isClean)
    {
        if (OpenFolderPickerAsync == null) return;

        var folder = await OpenFolderPickerAsync();
        if (folder != null)
        {
            string path = folder.Path.LocalPath;
            OpenCoreSnapshotService.ExecuteSnapshot(this, path, isClean);
        }
    }

    private void ParsePlistDocument(XDocument doc)
    {
        XElement? rootDict = doc.Root?.Element("dict");
        if (rootDict == null) return;

        FirstLevel.Clear();

        var elements = rootDict.Elements().ToList();
        for (int i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name.LocalName == "key")
            {
                string keyName = elements[i].Value;
                XElement valueElement = elements[i + 1];

                if (!keyName.StartsWith('#'))
                {
                    var section = PlistSectionViewModel.Create(keyName, valueElement, FirstLevel);
                    FirstLevel.Add(section);
                }
            }
        }

        SelectedSection = FirstLevel.FirstOrDefault();
    }

    private void GenerateSerials(string selectedModel)
    {
        var modelData = SmbiosGeneratorHelper.GenerateForModel(selectedModel);
        Smbios.PopulateFromModel(modelData);
        MarkDirty();
    }

    public void MarkDirty()
    {
        if (!IsDirty) IsDirty = true;
    }
}
