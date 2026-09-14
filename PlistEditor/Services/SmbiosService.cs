using System;
using System.Linq;
using PlistEditor.ViewModels;
using PlistEditor.Models;

namespace PlistEditor.Services;

public static class SmbiosService
{
    public static void InjectToPlatformInfo(MainViewModel mainVm, SmbiosModel smbios)
    {
        // 1. Find or create top-level 'PlatformInfo' section
        var platformInfo = mainVm.FirstLevel.FirstOrDefault(s => s.Name.Equals("PlatformInfo", StringComparison.OrdinalIgnoreCase));
        if (platformInfo == null)
        {
            platformInfo = new PlistSectionViewModel { Name = "PlatformInfo", ParentCollection = mainVm.FirstLevel };
            mainVm.FirstLevel.Add(platformInfo);
        }

        // 2. Find or create 'Generic' tab within PlatformInfo
        var genericTab = platformInfo.SubTabs.FirstOrDefault(t => t.Name.Equals("Generic", StringComparison.OrdinalIgnoreCase));
        if (genericTab == null)
        {
            genericTab = new PlistSectionViewModel { Name = "Generic", ParentCollection = platformInfo.SubTabs };
            platformInfo.SubTabs.Add(genericTab);
        }

        // 3. Populate keys inside Generic dictionary
        SetOrAddItem(genericTab, "SystemProductName", PlistItemType.String, smbios.ModelIdentifier);
        SetOrAddItem(genericTab, "SystemSerialNumber", PlistItemType.String, smbios.SystemSerialNumber);
        SetOrAddItem(genericTab, "MLB", PlistItemType.String, smbios.BoardSerialNumber);
        SetOrAddItem(genericTab, "SystemUUID", PlistItemType.String, smbios.SystemUUID);
        SetOrAddItem(genericTab, "ROM", PlistItemType.Data, smbios.Rom);

        mainVm.MarkDirty();
    }

    private static void SetOrAddItem(PlistSectionViewModel section, string key, PlistItemType type, object value)
    {
        var existing = section.Items.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            section.Items.Add(new PlistItemViewModel
            {
                Key = key,
                Type = type,
                Value = value,
                ParentCollection = section.Items
            });
        }
    }
}