using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    #region Fields

    public string WindowTitle => $"PlistEditor - {System.IO.Path.GetFileName(PlistFilePath)}{(IsDirty ? " *" : "")}";
    public bool HasLoadedPlist => !string.IsNullOrEmpty(PlistFilePath) && PlistFilePath != "No file opened";
    public ObservableCollection<PlistSectionViewModel> RootLevel { get; } = [];
    public ObservableCollection<PlistSectionViewModel> FirstLevel { get; } = [];

    #endregion

    #region Actions/Events

    public Func<Task<IStorageFile?>>? OpenFilePickerAsync { get; set; }
    public Func<Task<IStorageFolder?>>? OpenFolderPickerAsync { get; set; }
    public Func<Task<IStorageFile?>>? SaveFilePickerAsync { get; set; }
    public Func<Window?>? GetTopLevelWindow { get; set; }

    #endregion

    #region Properties

    [ObservableProperty] public partial PlistSectionViewModel? SelectedSection { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    public partial bool IsDirty { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(HasLoadedPlist))]
    public partial string PlistFilePath { get; set; } = "No file opened";

    [ObservableProperty] public partial SmbiosViewModel Smbios { get; set; } = new();

    #endregion

    #region Commands

    [RelayCommand]
    public void CreatePlist()
    {
        RootLevel.Clear();
        FirstLevel.Clear();
        PlistFilePath = "Untitled.plist";

        // Create the root node containing the top-level section
        var rootSection = new PlistSectionViewModel
        {
            Name = "Root",
            ParentCollection = null
        };

        RootLevel.Add(rootSection);

        SelectedSection = rootSection;
        rootSection.AttachChangeTracker(MarkDirty);
        IsDirty = true;
    }

    [RelayCommand]
    public async Task OpenPlistAsync()
    {
        if (OpenFilePickerAsync == null) return;

        var file = await OpenFilePickerAsync();
        if (file == null) return;

        try
        {
            PlistFilePath = file.Path.LocalPath;
            await using var stream = await file.OpenReadAsync();
            XDocument doc = await XDocument.LoadAsync(stream, LoadOptions.None, default);

            ParsePlistDocument(doc);
            IsDirty = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing plist: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task SavePlistAsync()
    {
        if (SaveFilePickerAsync == null || RootLevel.Count == 0) return;

        var file = await SaveFilePickerAsync();
        if (file == null) return;

        try
        {
            // Export root dictionary structure directly from RootLevel[0]
            XElement rootDict = RootLevel[0].ToXElement();

            XDocument doc = new(
                new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
                new XElement("plist", new XAttribute("version", "1.0"), rootDict)
            );

            await using var stream = await file.OpenWriteAsync();
            await doc.SaveAsync(stream, SaveOptions.None, default);

            IsDirty = false;
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

    [RelayCommand]
    public async Task OpenSettingsAsync()
    {
        //if (GetTopLevelWindow == null) return;
        //var owner = GetTopLevelWindow();
        //if (owner == null) return;

        //var dialog = new SettingsWindow();
        //var result = await dialog.ShowDialog<AppSettings?>(owner);
        //if (result != null)
        //{
        //    SettingsService.LoadSettings();
        //}
    }

    [RelayCommand]
    public static void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    #endregion

    public MainViewModel()
    {
        // Check if Avalonia is running in the Visual Studio XAML Designer
        if (Design.IsDesignMode)
        {
            LoadEmbeddedSample();
            PlistFilePath = "Sample.plist (Design Mode)";
        }
        else
        {
            CreatePlist();
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

        RootLevel.Clear();
        FirstLevel.Clear();

        // Create the primary root node
        var rootNode = PlistSectionViewModel.Create("Root", rootDict, null);
        RootLevel.Add(rootNode);

        // Population of first-level dictionary sections into FirstLevel for UI binding
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
                    section.AttachChangeTracker(MarkDirty);
                    FirstLevel.Add(section);
                    rootNode.SubTabs.Add(section);
                }
            }
        }

        SelectedSection = FirstLevel.FirstOrDefault();
    }

    public void MarkDirty()
    {
        if (!IsDirty) IsDirty = true;
    }
}
