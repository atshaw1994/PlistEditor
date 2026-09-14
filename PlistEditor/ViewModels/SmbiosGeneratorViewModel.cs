using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlistEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlistEditor.ViewModels;

public partial class SmbiosGeneratorViewModel : ObservableObject
{
    public MainViewModel? MainViewModelRef { get; }

    public SmbiosGeneratorViewModel()
    {
        // Default constructor for design-time support
        MainViewModelRef = new MainViewModel();
    }

    public SmbiosGeneratorViewModel(MainViewModel mainViewModel)
    {
        // Initialize with the main view model to access shared properties or methods if needed
        MainViewModelRef = mainViewModel;

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        string macserialPath = GetMacSerialPath();

        if (File.Exists(macserialPath))
        {
            MacSerialPath = macserialPath;
            StatusMessage = "Loading local Mac models...";
            await LoadModelsFromMacSerialAsync();
        }
        else
        {
        }
    }

    [ObservableProperty] public partial string MacSerialPath { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsDownloading { get; set; } = false;
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty] public partial string? SelectedCategory { get; set; } = string.Empty;
    [ObservableProperty] public partial string? SelectedModel { get; set; } = string.Empty;

    [ObservableProperty] public partial SmbiosModel? GeneratedResult { get; set; } = null;

    public ObservableCollection<string> Categories { get; } = [];
    public ObservableCollection<string> ModelsForCategory { get; } = [];

    // Dictionary mapping Category -> List of Models (e.g., "MacBookPro" -> ["MacBookPro16,1", ...])
    private readonly Dictionary<string, List<string>> _modelMap = new(StringComparer.OrdinalIgnoreCase);

    public bool CanGenerate => !string.IsNullOrEmpty(MacSerialPath) && File.Exists(MacSerialPath) && !string.IsNullOrEmpty(SelectedModel);

    partial void OnSelectedCategoryChanged(string? value)
    {
        ModelsForCategory.Clear();
        SelectedModel = null;

        if (value != null && _modelMap.TryGetValue(value, out var models))
        {
            foreach (var model in models)
            {
                ModelsForCategory.Add(model);
            }
        }
        OnPropertyChanged(nameof(CanGenerate));
    }

    partial void OnSelectedModelChanged(string? value)
    {
        OnPropertyChanged(nameof(CanGenerate));
    }

    [RelayCommand]
    public async Task DownloadMacSerialAsync()
    {
        IsDownloading = true;
        StatusMessage = "Fetching latest OpenCore release info...";

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "PlistEditor-App");

            string json = await client.GetStringAsync("https://api.github.com/repos/acidanthera/OpenCorePkg/releases/latest");
            using var doc = JsonDocument.Parse(json);

            string? downloadUrl = null;
            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name.Contains("RELEASE", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip"))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                StatusMessage = "Could not find a RELEASE zip in the latest OpenCore release.";
                return;
            }

            StatusMessage = "Downloading OpenCore archive...";
            byte[] zipData = await client.GetByteArrayAsync(downloadUrl);

            StatusMessage = "Extracting macserial binary...";

            string exeName = OperatingSystem.IsWindows() ? "macserial.exe" : "macserial";
            string targetFilePath = Path.Combine(AppContext.BaseDirectory, exeName);

            using (var zipStream = new MemoryStream(zipData))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                var macserialEntry = archive.Entries.FirstOrDefault(e =>
                    e.Name.Equals(exeName, StringComparison.OrdinalIgnoreCase) &&
                    e.FullName.Contains("macserial", StringComparison.OrdinalIgnoreCase));

                if (macserialEntry == null)
                {
                    StatusMessage = $"Could not locate {exeName} inside the release archive.";
                    return;
                }

                macserialEntry.ExtractToFile(targetFilePath, overwrite: true);
            }

            MacSerialPath = targetFilePath;

            if (!OperatingSystem.IsWindows())
            {
                Process.Start("chmod", $"+x \"{MacSerialPath}\"")?.WaitForExit();
            }

            StatusMessage = "Parsing available Mac models from macserial...";
            await LoadModelsFromMacSerialAsync();

            StatusMessage = "Ready.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error downloading macserial: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private async Task LoadModelsFromMacSerialAsync()
    {
        if (!File.Exists(MacSerialPath)) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = MacSerialPath,
            Arguments = "-l",
            RedirectStandardOutput = true,
            RedirectStandardError = true, // Consumes stderr so arc4random warnings don't pollute output
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = startInfo };
        proc.Start();
        string output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        _modelMap.Clear();
        Categories.Clear();

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            string trimmed = line.Trim();

            // Look specifically for lines formatted as "Model: MacBookPro16,1"
            if (!trimmed.StartsWith("Model:", StringComparison.OrdinalIgnoreCase)) continue;

            // Extract the model identifier (e.g., "MacBookPro16,1")
            string model = trimmed.Replace("Model:", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(model)) continue;

            // Extract leading letters for the category (e.g., "MacBookPro" from "MacBookPro16,1")
            string category = System.Text.RegularExpressions.Regex.Match(model, @"^[A-Za-z]+").Value;
            if (string.IsNullOrEmpty(category)) continue;

            if (!_modelMap.TryGetValue(category, out var modelList))
            {
                modelList = [];
                _modelMap[category] = modelList;
            }

            if (!modelList.Contains(model))
            {
                modelList.Add(model);
            }
        }

        foreach (var cat in _modelMap.Keys.OrderBy(k => k))
        {
            Categories.Add(cat);
        }

        OnPropertyChanged(nameof(CanGenerate));
    }

    [RelayCommand]
    public async Task GenerateAsync()
    {
        if (string.IsNullOrEmpty(SelectedModel) || !File.Exists(MacSerialPath)) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = MacSerialPath,
            Arguments = $"-m {SelectedModel} -n 1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = startInfo };
        proc.Start();
        string output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();

        // Output format: "SystemSerialNumber | MLB"
        var parts = output.Trim().Split('|');
        if (parts.Length >= 2)
        {
            byte[] romBytes = new byte[6];
            System.Security.Cryptography.RandomNumberGenerator.Fill(romBytes);

            GeneratedResult = new SmbiosModel
            {
                ModelIdentifier = SelectedModel,
                SystemSerialNumber = parts[0].Trim(),
                BoardSerialNumber = parts[1].Trim(),
                SystemUUID = Guid.NewGuid().ToString().ToUpper(),
                Rom = Convert.ToHexString(romBytes).ToUpper()
            };
        }
    }

    private static string GetMacSerialPath()
    {
        string exeName = OperatingSystem.IsWindows() ? "macserial.exe" : "macserial";
        return Path.Combine(AppContext.BaseDirectory, exeName);
    }
}
