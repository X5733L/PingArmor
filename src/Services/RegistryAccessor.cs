using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace PingArmor.Services;

/// <summary>
/// Abstraction over Windows registry access so that policy code can be unit-tested
/// without mutating the host machine.
/// </summary>
public interface IRegistryAccessor
{
    int? ReadDword(RegistryHive hive, string subKeyPath, string valueName);
    byte[]? ReadBinary(RegistryHive hive, string subKeyPath, string valueName);
    void WriteDword(RegistryHive hive, string subKeyPath, string valueName, int value);
    void WriteBinary(RegistryHive hive, string subKeyPath, string valueName, byte[] value);
    void DeleteValue(RegistryHive hive, string subKeyPath, string valueName);
}

/// <summary>
/// Real registry accessor used in production.
/// </summary>
public sealed class RegistryAccessor : IRegistryAccessor
{
    public static RegistryAccessor Default { get; } = new();

    public int? ReadDword(RegistryHive hive, string subKeyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKeyPath);
            var value = key?.GetValue(valueName);
            return value switch
            {
                int i => i,
                long l => unchecked((int)l),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public byte[]? ReadBinary(RegistryHive hive, string subKeyPath, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKeyPath);
            return key?.GetValue(valueName) as byte[];
        }
        catch
        {
            return null;
        }
    }

    public void WriteDword(RegistryHive hive, string subKeyPath, string valueName, int value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKeyPath, writable: true);
        key?.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    public void WriteBinary(RegistryHive hive, string subKeyPath, string valueName, byte[] value)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.CreateSubKey(subKeyPath, writable: true);
        key?.SetValue(valueName, value, RegistryValueKind.Binary);
    }

    public void DeleteValue(RegistryHive hive, string subKeyPath, string valueName)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
        using var key = baseKey.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}

/// <summary>
/// In-memory registry accessor for tests. Records writes and deletions.
/// </summary>
public sealed class InMemoryRegistryAccessor : IRegistryAccessor
{
    private readonly Dictionary<string, int> _dwords = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _binaries = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, int> Dwords => _dwords;
    public IReadOnlyDictionary<string, byte[]> Binaries => _binaries;

    private static string Key(RegistryHive hive, string subKeyPath, string valueName) =>
        $"{hive}\\{subKeyPath}\\{valueName}";

    public int? ReadDword(RegistryHive hive, string subKeyPath, string valueName)
        => _dwords.TryGetValue(Key(hive, subKeyPath, valueName), out int value) ? value : null;

    public byte[]? ReadBinary(RegistryHive hive, string subKeyPath, string valueName)
        => _binaries.TryGetValue(Key(hive, subKeyPath, valueName), out var value) ? value : null;

    public void WriteDword(RegistryHive hive, string subKeyPath, string valueName, int value)
        => _dwords[Key(hive, subKeyPath, valueName)] = value;

    public void WriteBinary(RegistryHive hive, string subKeyPath, string valueName, byte[] value)
        => _binaries[Key(hive, subKeyPath, valueName)] = value;

    public void DeleteValue(RegistryHive hive, string subKeyPath, string valueName)
    {
        _dwords.Remove(Key(hive, subKeyPath, valueName));
        _binaries.Remove(Key(hive, subKeyPath, valueName));
    }
}
