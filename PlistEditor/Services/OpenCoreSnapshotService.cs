using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PlistEditor.ViewModels;

namespace PlistEditor.Services;

public static class OpenCoreSnapshotService
{
    public static void ExecuteSnapshot(MainViewModel mainVm, string efiFolderPath, bool isClean)
    {
        string ocPath = Path.Combine(efiFolderPath, "OC");
        if (!Directory.Exists(ocPath))
        {
            // If user selected OC folder directly instead of EFI parent
            if (Path.GetFileName(efiFolderPath).Equals("OC", StringComparison.OrdinalIgnoreCase))
            {
                ocPath = efiFolderPath;
            }
            else
            {
                throw new DirectoryNotFoundException("Could not locate the 'OC' directory in the selected folder.");
            }
        }

        // Process the 4 core OpenCore folders
        ProcessSection(mainVm, "ACPI", Path.Combine(ocPath, "ACPI"), ".aml", isClean);
        ProcessSection(mainVm, "Booter", Path.Combine(ocPath, "Drivers"), ".efi", isClean, isBooterDrivers: true); // Drivers live under UEFI or Booter depending on OC version
        ProcessSection(mainVm, "Kernel", Path.Combine(ocPath, "Kexts"), ".kext", isClean);
        ProcessSection(mainVm, "Misc", Path.Combine(ocPath, "Tools"), ".efi", isClean, isTools: true);

        mainVm.MarkDirty();
    }

    private static void ProcessSection(MainViewModel mainVm, string sectionName, string folderPath, string extension, bool isClean, bool isBooterDrivers = false, bool isTools = false)
    {
        if (!Directory.Exists(folderPath)) return;

        // Locate or create the top-level section (e.g., Kernel, ACPI)
        var section = mainVm.FirstLevel.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.OrdinalIgnoreCase));
        if (section == null)
        {
            section = new PlistSectionViewModel { Name = sectionName, ParentCollection = mainVm.FirstLevel };
            mainVm.FirstLevel.Add(section);
        }

        // Find the "Add" array inside the section
        var addTab = section.SubTabs.FirstOrDefault(t => t.Name.Equals("Add", StringComparison.OrdinalIgnoreCase));
        if (addTab == null)
        {
            addTab = new PlistSectionViewModel { Name = "Add", ParentCollection = section.SubTabs };
            section.SubTabs.Add(addTab);
        }

        if (isClean)
        {
            addTab.Items.Clear();
        }

        // Gather existing entries to prevent duplicate insertion
        var existingPaths = new HashSet<string>(
            addTab.Items.Select(item => GetItemPath(item)).Where(p => !string.IsNullOrEmpty(p)),
            StringComparer.OrdinalIgnoreCase
        );

        // Scan physical files
        var foundFiles = Directory.GetFileSystemEntries(folderPath)
            .Where(f => f.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                       (extension == ".kext" && Directory.Exists(f)))
            .Select(f => Path.GetFileName(f))
            .ToList();

        foreach (var fileName in foundFiles)
        {
            if (!existingPaths.Contains(fileName))
            {
                addTab.Items.Add(CreateSnapshotItem(fileName, addTab.Items, isTools));
            }
        }
    }

    private static string GetItemPath(PlistItemViewModel item)
    {
        var pathChild = item.Children.FirstOrDefault(c => c.Key.Equals("Path", StringComparison.OrdinalIgnoreCase) || c.Key.Equals("BundlePath", StringComparison.OrdinalIgnoreCase));
        return pathChild?.Value?.ToString() ?? string.Empty;
    }

    private static PlistItemViewModel CreateSnapshotItem(string fileName, ObservableCollection<PlistItemViewModel> parent, bool isTool)
    {
        var dictItem = new PlistItemViewModel
        {
            Key = $"Item {parent.Count}",
            Type = PlistItemType.Dictionary,
            Value = "{ Dict }",
            ParentCollection = parent
        };

        bool isKext = fileName.EndsWith(".kext", StringComparison.OrdinalIgnoreCase);

        if (isKext)
        {
            dictItem.Children.Add(new PlistItemViewModel { Key = "Arch", Type = PlistItemType.String, Value = "Any", ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "BundlePath", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "Comment", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "Enabled", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "ExecutablePath", Type = PlistItemType.String, Value = GetKextExecutablePath(fileName), ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "MaxKernel", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "MinKernel", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "PlistPath", Type = PlistItemType.String, Value = "Contents/Info.plist", ParentCollection = dictItem.Children });
        }
        else
        {
            dictItem.Children.Add(new PlistItemViewModel { Key = "Comment", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "Enabled", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
            dictItem.Children.Add(new PlistItemViewModel { Key = "Path", Type = PlistItemType.String, Value = fileName, ParentCollection = dictItem.Children });

            if (isTool)
            {
                dictItem.Children.Add(new PlistItemViewModel { Key = "Arguments", Type = PlistItemType.String, Value = "", ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Auxiliary", Type = PlistItemType.Boolean, Value = true, ParentCollection = dictItem.Children });
                dictItem.Children.Add(new PlistItemViewModel { Key = "Flavour", Type = PlistItemType.String, Value = "Auto", ParentCollection = dictItem.Children });
            }
        }

        return dictItem;
    }

    private static string GetKextExecutablePath(string kextName)
    {
        // Common kext executable path pattern (e.g., VirtualSMC.kext/Contents/MacOS/VirtualSMC)
        string pureName = Path.GetFileNameWithoutExtension(kextName);
        return $"Contents/MacOS/{pureName}";
    }
}