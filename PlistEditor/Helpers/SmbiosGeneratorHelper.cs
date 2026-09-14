using System;
using System.Text;
using PlistEditor.Models;

namespace PlistEditor.Helpers;

public static class SmbiosGeneratorHelper
{
    private static readonly Random _random = new();
    private const string Base34Chars = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public static SmbiosModel GenerateForModel(string model)
    {
        string systemSerial = GenerateSystemSerial(model);
        string boardSerial = GenerateBoardSerial(systemSerial, model);

        return new SmbiosModel
        {
            ModelIdentifier = model,
            SystemSerialNumber = systemSerial,
            BoardSerialNumber = boardSerial,
            SystemUUID = Guid.NewGuid().ToString().ToUpper(),
            Rom = GenerateRandomMacAddress()
        };
    }

    public static string GenerateSystemSerial(string model)
    {
        string location = "C02";
        string yearWeek = "C9";
        string uniqueCode = GetRandomBase34(3);
        string modelCode = GetModelCode(model);

        return $"{location}{yearWeek}{uniqueCode}{modelCode}";
    }

    public static string GenerateBoardSerial(string systemSerial, string model)
    {
        if (systemSerial.Length < 12) return string.Empty;

        string prefix = systemSerial[..8];
        string boardCode = GetBoardCode(model);
        string suffix = GetRandomBase34(5);

        return $"{prefix}{boardCode}{suffix}";
    }

    public static string GenerateRandomMacAddress()
    {
        byte[] bytes = new byte[6];
        _random.NextBytes(bytes);
        bytes[0] = (byte)((bytes[0] & 0xFE) | 0x02);
        return Convert.ToHexString(bytes);
    }

    private static string GetRandomBase34(int length)
    {
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            sb.Append(Base34Chars[_random.Next(Base34Chars.Length)]);
        }
        return sb.ToString();
    }

    private static string GetModelCode(string model) => model switch
    {
        "MacBookPro16,1" => "MD6N",
        "iMac19,1" => "JV3N",
        "iMacPro1,1" => "HX87",
        "Macmini8,1" => "JYVY",
        _ => "0000"
    };

    private static string GetBoardCode(string model) => model switch
    {
        "MacBookPro16,1" => "100Q",
        "iMac19,1" => "1018",
        "iMacPro1,1" => "1019",
        "Macmini8,1" => "1011",
        _ => "0000"
    };
}