using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PlistEditor.ViewModels;

public enum PlistItemType
{
    String,
    Integer,
    Boolean,
    Data,
    Array,
    Dictionary
}

public partial class PlistItemViewModel : ObservableValidator
{
    public PlistItemViewModel()
    {
        // Re-evaluate display and notify parent when children change
        Children.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(DisplayName));
            OnChanged?.Invoke();
        };
    }

    public Action? OnChanged { get; set; }

    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial object? Value { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStringOrInt))]
    [NotifyPropertyChangedFor(nameof(IsBoolean))]
    [NotifyPropertyChangedFor(nameof(IsData))]
    [NotifyPropertyChangedFor(nameof(ValueAsHex))]
    public partial PlistItemType Type { get; set; } = PlistItemType.String;

    [ObservableProperty]
    public partial bool IsEditing { get; set; } = false;

    /// <summary>
    /// Gets the formatted display label. If a child with key "Comment" exists, 
    /// appends its string value in parentheses.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var commentChild = Children.FirstOrDefault(c => string.Equals(c.Key, "Comment", StringComparison.OrdinalIgnoreCase));
            var bundlePathChild = Children.FirstOrDefault(c => string.Equals(c.Key, "BundlePath", StringComparison.OrdinalIgnoreCase));
            var pathChild = Children.FirstOrDefault(c => string.Equals(c.Key, "Path", StringComparison.OrdinalIgnoreCase));
            string? commentValue = commentChild?.Value?.ToString();
            string? bundlePathValue = bundlePathChild?.Value?.ToString();
            string? pathValue = pathChild?.Value?.ToString();


            if (!string.IsNullOrWhiteSpace(bundlePathValue))
            {
                return $"{Key} ({System.IO.Path.GetFileNameWithoutExtension(bundlePathValue.Split('/')[0])})";
            }

            if (!string.IsNullOrWhiteSpace(pathValue))
            {
                return $"{Key} ({System.IO.Path.GetFileNameWithoutExtension(pathValue.Split('/')[0])})";
            }

            if (!string.IsNullOrWhiteSpace(commentValue))
            {
                return $"{Key} ({commentValue})";
            }

            return Key;
        }
    }

    public ObservableCollection<PlistItemViewModel> Children { get; } = [];
    public ObservableCollection<PlistItemViewModel>? ParentCollection { get; set; }

    public static Array AvailableTypes => Enum.GetValues<PlistItemType>();

    public bool IsStringOrInt => Type == PlistItemType.String || Type == PlistItemType.Integer;
    public bool IsBoolean => Type == PlistItemType.Boolean;
    public bool IsData => Type == PlistItemType.Data;

    // Direct, loop-safe computed wrapper property for CheckBox bindings
    public bool ValueAsBool
    {
        get => Value is bool b ? b : (bool.TryParse(Value?.ToString(), out bool parsed) && parsed);
        set
        {
            if (Type == PlistItemType.Boolean && !Equals(Value, value))
            {
                Value = value;
                OnPropertyChanged();
            }
        }
    }

    // Computed wrapper for Data display/editing
    public string ValueAsHex
    {
        get => Value is string s ? $"<{s}>" : "<>";
        set
        {
            string clean = value.Replace("<", "").Replace(">", "").Trim();
            if (!Equals(Value, clean))
            {
                Value = clean;
                OnPropertyChanged();
            }
        }
    }

    partial void OnTypeChanged(PlistItemType value)
    {
        switch (value)
        {
            case PlistItemType.Boolean:
                // Only force default if Value hasn't been parsed as bool yet
                if (Value is not bool)
                {
                    Value = false;
                }
                OnPropertyChanged(nameof(ValueAsBool));
                OnChanged?.Invoke();
                break;
            case PlistItemType.Integer:
                if (Value is not long)
                {
                    Value = long.TryParse(Value?.ToString(), out long i) ? i : 0L;
                }
                OnChanged?.Invoke();
                break;
            case PlistItemType.Data:
                if (Value is not string str || !IsHexFormat(str))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(Value?.ToString() ?? string.Empty);
                    Value = Convert.ToHexString(bytes);
                }
                OnChanged?.Invoke();
                break;
            case PlistItemType.String:
                if (Value is not string)
                {
                    Value = Value?.ToString() ?? string.Empty;
                }
                OnChanged?.Invoke();
                break;
        }
    }
    partial void OnKeyChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnChanged?.Invoke();
    }
    partial void OnValueChanged(object? value)
    {
        OnPropertyChanged(nameof(DisplayName));
        OnChanged?.Invoke();
    }

    public static PlistItemViewModel FromXElement(string key, XElement element, ObservableCollection<PlistItemViewModel>? parent = null)
    {
        var (type, val) = element.Name.LocalName switch
        {
            "dict" => (PlistItemType.Dictionary, (object)"{ Dict }"),
            "array" => (PlistItemType.Array, (object)$"Array [{element.Elements().Count()}]"),
            "integer" => (PlistItemType.Integer, long.TryParse(element.Value, out long i) ? i : 0L),
            "true" => (PlistItemType.Boolean, (object)true),
            "false" => (PlistItemType.Boolean, (object)false),
            "data" => (PlistItemType.Data, TryParseHex(element.Value)),
            _ => (PlistItemType.String, (object)element.Value)
        };

        var node = new PlistItemViewModel
        {
            Key = key,
            Type = type,
            Value = val,
            ParentCollection = parent
        };

        if (element.Name.LocalName == "dict")
        {
            var dictElements = element.Elements().ToList();
            for (int i = 0; i < dictElements.Count - 1; i++)
            {
                if (dictElements[i].Name.LocalName == "key")
                {
                    string childKey = dictElements[i].Value;
                    XElement childVal = dictElements[i + 1];
                    node.Children.Add(FromXElement(childKey, childVal, node.Children));
                }
            }
        }
        else if (element.Name.LocalName == "array")
        {
            var arrayItems = element.Elements().ToList();
            for (int i = 0; i < arrayItems.Count; i++)
            {
                node.Children.Add(FromXElement($"Item {i}", arrayItems[i], node.Children));
            }
        }

        return node;
    }

    private static string TryParseHex(string rawValue)
    {
        try
        {
            byte[] base64Bytes = Convert.FromBase64String(rawValue.Trim());
            return Convert.ToHexString(base64Bytes);
        }
        catch
        {
            return rawValue;
        }
    }

    public XElement ToXElement()
    {
        return Type switch
        {
            PlistItemType.Dictionary => new XElement("dict", Children.SelectMany(c => new XNode[] { new XElement("key", c.Key), c.ToXElement() })),
            PlistItemType.Array => new XElement("array", Children.Select(c => c.ToXElement())),
            PlistItemType.Integer => new XElement("integer", Value?.ToString() ?? "0"),
            PlistItemType.Boolean => new XElement(Value is true ? "true" : "false"),
            PlistItemType.Data => new XElement("data", ConvertHexToBase64(Value?.ToString())),
            _ => new XElement("string", Value?.ToString() ?? string.Empty)
        };
    }

    private static string ConvertHexToBase64(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return string.Empty;
        try
        {
            string cleanHex = hex.Replace(" ", "").Replace("<", "").Replace(">", "");
            byte[] rawBytes = Convert.FromHexString(cleanHex);
            return Convert.ToBase64String(rawBytes);
        }
        catch
        {
            return hex;
        }
    }

    private static bool IsHexFormat(string str) => str.All("0123456789abcdefABCDEF ".Contains);

    [RelayCommand]
    public void AddChild()
    {
        Children.Add(new PlistItemViewModel
        {
            Key = Type == PlistItemType.Array ? $"Item {Children.Count}" : "NewKey",
            Value = "NewValue",
            Type = PlistItemType.String,
            ParentCollection = Children
        });
    }

    [RelayCommand]
    public void Delete() => ParentCollection?.Remove(this);

    [RelayCommand]
    public void ToggleEdit() => IsEditing = !IsEditing;

}