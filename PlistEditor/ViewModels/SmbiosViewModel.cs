using CommunityToolkit.Mvvm.ComponentModel;
using PlistEditor.Models;

namespace PlistEditor.ViewModels;

public partial class SmbiosViewModel : ObservableObject
{
    [ObservableProperty] public partial string ModelIdentifier { get; set; } = string.Empty;
    [ObservableProperty] public partial string SystemSerialNumber { get; set; } = string.Empty;
    [ObservableProperty] public partial string BoardSerialNumber { get; set; } = string.Empty;
    [ObservableProperty] public partial string SystemUUID { get; set; } = string.Empty;
    [ObservableProperty] public partial string Rom { get; set; } = string.Empty;

    public void PopulateFromModel(SmbiosModel model)
    {
        ModelIdentifier = model.ModelIdentifier;
        SystemSerialNumber = model.SystemSerialNumber;
        BoardSerialNumber = model.BoardSerialNumber;
        SystemUUID = model.SystemUUID;
        Rom = model.Rom;
    }

    public SmbiosModel ToModel() => new()
    {
        ModelIdentifier = ModelIdentifier,
        SystemSerialNumber = SystemSerialNumber,
        BoardSerialNumber = BoardSerialNumber,
        SystemUUID = SystemUUID,
        Rom = Rom
    };
}