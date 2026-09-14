namespace PlistEditor.Models;

public class SmbiosModel
{
    public string ModelIdentifier { get; set; } = string.Empty;
    public string SystemSerialNumber { get; set; } = string.Empty;
    public string BoardSerialNumber { get; set; } = string.Empty; // MLB
    public string SystemUUID { get; set; } = string.Empty;
    public string Rom { get; set; } = string.Empty;
}
