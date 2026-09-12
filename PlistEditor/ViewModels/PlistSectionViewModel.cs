using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PlistEditor.ViewModels;

public partial class PlistSectionViewModel : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    public ObservableCollection<PlistSectionViewModel> SubTabs { get; } = [];
    public ObservableCollection<PlistItemViewModel> Items { get; } = [];

    public bool HasSubTabs => SubTabs.Count > 0;

    // --- CRUD Commands for Tab/Section Level ---

    [RelayCommand]
    public void AddItem()
    {
        Items.Add(new PlistItemViewModel
        {
            Key = "NewKey",
            Value = "NewValue",
            Type = PlistItemType.String,
            ParentCollection = Items
        });
    }

    [RelayCommand]
    public void AddSubTab()
    {
        SubTabs.Add(new PlistSectionViewModel
        {
            Name = "NewDict"
        });
        OnPropertyChanged(nameof(HasSubTabs));
    }

    public static PlistSectionViewModel Create(string name, XElement element)
    {
        var section = new PlistSectionViewModel { Name = name };

        if (element.Name.LocalName == "dict")
        {
            var elements = element.Elements().ToList();
            for (int i = 0; i < elements.Count - 1; i++)
            {
                if (elements[i].Name.LocalName == "key")
                {
                    string childKey = elements[i].Value;
                    XElement childValue = elements[i + 1];

                    // Accept both nested dicts AND arrays as top-level sub-tabs
                    if (childValue.Name.LocalName == "dict" || childValue.Name.LocalName == "array")
                    {
                        section.SubTabs.Add(Create(childKey, childValue));
                    }
                    else
                    {
                        section.Items.Add(PlistItemViewModel.FromXElement(childKey, childValue, section.Items));
                    }
                }
            }
        }
        else if (element.Name.LocalName == "array")
        {
            // Treat array items as regular children inside this tab
            var arrayItems = element.Elements().ToList();
            for (int i = 0; i < arrayItems.Count; i++)
            {
                section.Items.Add(PlistItemViewModel.FromXElement($"Item {i}", arrayItems[i], section.Items));
            }
        }
        else
        {
            section.Items.Add(PlistItemViewModel.FromXElement(name, element, section.Items));
        }

        return section;
    }

    public XElement ToXElement()
    {
        var dict = new XElement("dict");

        // Output child sub-dictionaries (Tabs)
        foreach (var subTab in SubTabs)
        {
            dict.Add(new XElement("key", subTab.Name));
            dict.Add(subTab.ToXElement());
        }

        // Output leaf items / arrays
        foreach (var item in Items)
        {
            dict.Add(new XElement("key", item.Key));
            dict.Add(item.ToXElement());
        }

        return dict;
    }
}