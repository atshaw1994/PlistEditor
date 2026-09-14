using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PlistEditor.Services;

public class GeneratedSerialResult
{
    public string Model { get; set; } = string.Empty;
    public string SystemSerialNumber { get; set; } = string.Empty;
    public string MLB { get; set; } = string.Empty;
}

public static class MacSerialService
{
    public static async Task<GeneratedSerialResult?> GenerateSerialAsync(string macserialPath, string modelName)
    {
        if (!File.Exists(macserialPath))
            throw new FileNotFoundException("macserial binary not found.", macserialPath);

        // Usage: macserial -m <Model> -n 1
        var startInfo = new ProcessStartInfo
        {
            FileName = macserialPath,
            Arguments = $"-m {modelName} -n 1",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (string.IsNullOrWhiteSpace(output)) return null;

        // Output format: "SystemSerialNumber | MLB"
        // Example: C02CG000MD6M | C020123004500001A
        var parts = output.Trim().Split('|');
        if (parts.Length >= 2)
        {
            return new GeneratedSerialResult
            {
                Model = modelName,
                SystemSerialNumber = parts[0].Trim(),
                MLB = parts[1].Trim()
            };
        }

        return null;
    }
}