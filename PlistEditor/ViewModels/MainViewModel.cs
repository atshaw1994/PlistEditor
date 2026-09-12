using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PlistEditor.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ObservableCollection<PlistSectionViewModel> FirstLevel { get; } = [];

    public Func<Task<IStorageFile?>>? OpenFilePickerAsync { get; set; }
    public Func<Task<IStorageFile?>>? SaveFilePickerAsync { get; set; }

    [ObservableProperty] public partial PlistSectionViewModel? SelectedSection { get; set; }
    [ObservableProperty] public partial string PlistFilePath { get; set; } = "No file opened";

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
                        FirstLevel.Add(PlistSectionViewModel.Create(keyName, valueElement));
                    }
                }
            }

            SelectedSection = FirstLevel.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing plist: {ex.Message}");
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving plist: {ex.Message}");
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
                    FirstLevel.Add(PlistSectionViewModel.Create(keyName, valueElement));
                }
            }
        }

        SelectedSection = FirstLevel.FirstOrDefault();
    }
}
