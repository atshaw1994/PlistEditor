using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Xml.Linq;

namespace PlistEditor.ViewModels;

public partial class PlistSectionViewModel : ObservableObject
{
    public Action? OnChanged { get; set; }

    [ObservableProperty] public partial string Name { get; set; } = string.Empty;
    partial void OnNameChanged(string value) => OnChanged?.Invoke();
    [ObservableProperty] public partial bool IsEditing { get; set; } = false;

    public ObservableCollection<PlistSectionViewModel> SubTabs { get; } = [];
    public ObservableCollection<PlistItemViewModel> Items { get; } = [];

    public bool HasSubTabs => SubTabs.Count > 0;

    // Reference to the parent list containing this tab (so it can remove itself)
    public ObservableCollection<PlistSectionViewModel>? ParentCollection { get; set; }

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
        var newSub = new PlistSectionViewModel
        {
            Name = "NewDict",
            ParentCollection = SubTabs // <-- Crucial so it knows how to delete itself from its parent
        };
        SubTabs.Add(newSub);
        OnPropertyChanged(nameof(HasSubTabs));
    }

    [RelayCommand]
    public void ToggleEdit() => IsEditing = !IsEditing;

    [RelayCommand]
    public void Delete()
    {
        ParentCollection?.Remove(this);
        OnPropertyChanged(nameof(HasSubTabs));
        OnChanged?.Invoke();
    }

    public static PlistSectionViewModel Create(string name, XElement element, ObservableCollection<PlistSectionViewModel>? parent = null)
    {
        var section = new PlistSectionViewModel
        {
            Name = name,
            ParentCollection = parent
        };

        if (element.Name.LocalName == "dict")
        {
            var elements = element.Elements().ToList();
            for (int i = 0; i < elements.Count - 1; i++)
            {
                if (elements[i].Name.LocalName == "key")
                {
                    string childKey = elements[i].Value;
                    XElement childValue = elements[i + 1];

                    if (childValue.Name.LocalName == "dict" || childValue.Name.LocalName == "array")
                    {
                        section.SubTabs.Add(Create(childKey, childValue, section.SubTabs));
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
            var arrayItems = element.Elements().ToList();
            for (int i = 0; i < arrayItems.Count; i++)
            {
                section.Items.Add(PlistItemViewModel.FromXElement($"Item {i}", arrayItems[i], section.Items));
            }
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

    public PlistSectionViewModel()
    {
        SubTabs.CollectionChanged += (s, e) => OnChanged?.Invoke();
        Items.CollectionChanged += (s, e) => OnChanged?.Invoke();
    }

    public void AttachChangeTracker(Action onChange)
    {
        OnChanged = onChange;
        foreach (var sub in SubTabs) sub.AttachChangeTracker(onChange);
        foreach (var item in Items) AttachItemTracker(item, onChange);
    }

    private static void AttachItemTracker(PlistItemViewModel item, Action onChange)
    {
        item.OnChanged = onChange;
        foreach (var child in item.Children) AttachItemTracker(child, onChange);
    }
}