using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PlistEditor.ViewModels;

namespace PlistEditor.Services;

public static class OpenCoreSnapshotService
{
    public static void ExecuteSnapshot(MainViewModel mainVm, string selectedFolderPath, bool isClean)
    {
        string ocPath = selectedFolderPath;

        // Check if user selected the parent directory containing "OC" (e.g. EFI/ or root)
        string candidateOcSubfolder = Path.Combine(selectedFolderPath, "OC");
        if (Directory.Exists(candidateOcSubfolder))
        {
            ocPath = candidateOcSubfolder;
        }
        // Check if user selected "EFI" which contains "OC" (e.g. root folder -> EFI -> OC)
        else
        {
            string candidateEfiOc = Path.Combine(selectedFolderPath, "EFI", "OC");
            if (Directory.Exists(candidateEfiOc))
            {
                ocPath = candidateEfiOc;
            }
        }

        // ocPath now points directly to the contents folder (holding ACPI, Drivers, Kexts, Tools)
        ProcessSection(mainVm, "ACPI", "Add", Path.Combine(ocPath, "ACPI"), ".aml", isClean, ItemKind.ACPI);
        ProcessSection(mainVm, "Kernel", "Add", Path.Combine(ocPath, "Kexts"), ".kext", isClean, ItemKind.Kext);
        ProcessSection(mainVm, "Misc", "Tools", Path.Combine(ocPath, "Tools"), ".efi", isClean, ItemKind.Tool);
        ProcessSection(mainVm, "UEFI", "Drivers", Path.Combine(ocPath, "Drivers"), ".efi", isClean, ItemKind.Driver);

        mainVm.MarkDirty();
    }

    private enum ItemKind { ACPI, Kext, Tool, Driver }

    private static void ProcessSection(MainViewModel mainVm, string rootSectionName, string subTabName, string folderPath, string extension, bool isClean, ItemKind kind)
    {
        if (!Directory.Exists(folderPath)) return;

        // Locate or create Top-Level Root Section (e.g., UEFI, ACPI, Kernel, Misc)
        var rootSection = mainVm.FirstLevel.FirstOrDefault(s => s.Name.Equals(rootSectionName, StringComparison.OrdinalIgnoreCase));
        if (rootSection == null)
        {
            rootSection = new PlistSectionViewModel { Name = rootSectionName, ParentCollection = mainVm.FirstLevel };
            mainVm.FirstLevel.Add(rootSection);
        }

        // Locate or create Sub-Tab (e.g., UEFI -> Drivers, Kernel -> Add)
        var targetTab = rootSection.SubTabs.FirstOrDefault(t => t.Name.Equals(subTabName, StringComparison.OrdinalIgnoreCase));
        if (targetTab == null)
        {
            targetTab = new PlistSectionViewModel { Name = subTabName, ParentCollection = rootSection.SubTabs };
            rootSection.SubTabs.Add(targetTab);
            rootSection.NotifyHasSubTabsChanged();
        }

        if (isClean)
        {
            targetTab.Items.Clear();
        }

        // Map existing entries to avoid duplicates
        var existingPaths = new HashSet<string>(
            targetTab.Items.Select(GetItemPath).Where(p => !string.IsNullOrEmpty(p)),
            StringComparer.OrdinalIgnoreCase
        );

        // Gather files matching extension or bundle folders
        var foundFiles = Directory.GetFileSystemEntries(folderPath)
            .Where(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                       (kind == ItemKind.Kext && Directory.Exists(f)))
            .Select(Path.GetFileName)
            .ToList();

        foreach (var fileName in foundFiles)
        {
            if (!string.IsNullOrEmpty(fileName) && !existingPaths.Contains(fileName))
            {
                targetTab.Items.Add(CreateSnapshotItem(fileName, targetTab.Items, kind));
            }
        }
    }

    private static string GetItemPath(PlistItemViewModel item)
    {
        // Drivers in UEFI can be raw Strings (legacy) or Dicts with a Path key
        if (item.Type == PlistItemType.String)
        {
            return item.Value?.ToString() ?? string.Empty;
        }

        var pathChild = item.Children.FirstOrDefault(c =>
            c.Key.Equals("Path", StringComparison.OrdinalIgnoreCase) ||
            c.Key.Equals("BundlePath", StringComparison.OrdinalIgnoreCase));

        return pathChild?.Value?.ToString() ?? string.Empty;
    }

    private static PlistItemViewModel CreateSnapshotItem(string fileName, ObservableCollection<PlistItemViewModel> parent, ItemKind kind)
    {
        var dictItem = new PlistItemViewModel
        {
            Key = $"Item {parent.Count}",
            Type = PlistItemType.Dictionary,
            Value = "{ Dict }",
            ParentCollection = parent
        };

        switch (kind)
        {
            case ItemKind.Driver:
                dictItem.Children.Add(new PlistItemViewModel { Key = "Arguments", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Comment", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Enabled", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "LoadEarly", Type = PlistItemType.Boolean, Value = false, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Path", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });
                break;

            case ItemKind.Kext:
                string pureName = Path.GetFileNameWithoutExtension(fileName);
                dictItem.Children.Add(new PlistItemViewModel { Key = "Arch", Type = PlistItemType.String, Value = "Any", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "BundlePath", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Comment", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Enabled", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "ExecutablePath", Type = PlistItemType.String, Value = $"Contents/MacOS/{pureName}", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "MaxKernel", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "MinKernel", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "PlistPath", Type = PlistItemType.String, Value = "Contents/Info.plist", ParentCollection = dictItem.Children });
                break;

            case ItemKind.Tool:
                dictItem.Children.Add(new PlistItemViewModel { Key = "Arguments", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Auxiliary", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Comment", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Enabled", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Flavour", Type = PlistItemType.String, Value = "Auto", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Name", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Path", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "RealPath", Type = PlistItemType.Boolean, Value = false, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "TextMode", Type = PlistItemType.Boolean, Value = false, ParentCollection = dictItem.Children });
                break;

            case ItemKind.ACPI:
                dictItem.Children.Add(new PlistItemViewModel { Key = "Comment", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Enabled", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Path", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });
                break;
        }

        return dictItem;
    }
}