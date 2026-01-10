using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using HonestMainMenu.Models;

#if IL2CPP_BUILD
using Il2CppScheduleOne.Clothing;
using ColorDataList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Clothing.ClothingUtility.ColorData>;
#elif MONO_BUILD
using ScheduleOne.Clothing;
using ColorDataList = System.Collections.Generic.List<ScheduleOne.Clothing.ClothingUtility.ColorData>;
#endif

namespace HonestMainMenu.Services;

/// <summary>
/// Handles loading and saving clothing color data (from disk or embedded resource) without extra caches.
/// </summary>
internal static class ColorDataRepository
{
    private const string ResourceName = "HonestMainMenu.Resources.ColorData.json";
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly DataContractJsonSerializerSettings SerializerSettings =
        new() { UseSimpleDictionaryFormat = true };
    private static readonly DataContractJsonSerializer ColorListSerializer =
        new(typeof(List<SerializableColorData>), SerializerSettings);

    internal static string UserDataPath =>
        Path.Combine(MelonEnvironment.MelonBaseDirectory, "Mods", "HonestMainMenu");

    internal static string ColorDataFilePath => Path.Combine(UserDataPath, "ColorData.json");

    internal static List<SerializableColorData> Load()
    {
        if (File.Exists(ColorDataFilePath))
        {
            try
            {
                using FileStream stream = File.OpenRead(ColorDataFilePath);
                return ReadColorList(stream, ColorDataFilePath);
            }
            catch (Exception ex)
            {
                Melon<Main>.Logger.Error(
                    $"[ColorDataRepository] Failed to read custom color data '{ColorDataFilePath}': {ex}"
                );
            }
        }

        Melon<Main>.Logger.Msg(
            "[ColorDataRepository] Custom color data not found or unreadable. Loading embedded defaults."
        );
        return LoadEmbeddedDefaults();
    }

    internal static void SaveLiveColors(ColorDataList liveDataList)
    {
        if (liveDataList == null)
        {
            Melon<Main>.Logger.Warning(
                "[ColorDataRepository] No live color data provided; skipping save."
            );
            return;
        }

        try
        {
            var collected = new List<SerializableColorData>(liveDataList.Count);
            foreach (var colorData in liveDataList)
            {
                if (colorData == null)
                    continue;
                collected.Add(new SerializableColorData(colorData.ColorType, colorData.ActualColor));
            }

            var defaults = LoadEmbeddedDefaults(cloneList: false);
            if (!collected.SequenceEqual(defaults))
            {
                Melon<Main>.Logger.Warning(
                    "[ColorDataRepository] Live color data differs from embedded defaults. Consider updating Resources/ColorData.json."
                );
            }
            else
            {
                Melon<Main>.Logger.Msg(
                    "[ColorDataRepository] Live color data matches embedded defaults."
                );
            }

            EnsureUserDataDirectory();
            string json = SerializeColors(collected);
            File.WriteAllText(ColorDataFilePath, json, Utf8);
            Melon<Main>.Logger.Msg(
                $"[ColorDataRepository] Saved {collected.Count} colors to '{ColorDataFilePath}'."
            );
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error($"[ColorDataRepository] Failed to save live color data: {ex}");
        }
    }

    internal static List<SerializableColorData> LoadEmbeddedDefaults(bool cloneList = true)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using Stream stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream == null)
            {
                Melon<Main>.Logger.Error(
                    $"[ColorDataRepository] Embedded resource '{ResourceName}' not found."
                );
                return new();
            }

            var defaults = ReadColorList(stream, ResourceName);
            return cloneList ? new List<SerializableColorData>(defaults) : defaults;
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error(
                $"[ColorDataRepository] Failed to read embedded color data '{ResourceName}': {ex}"
            );
            return new();
        }
    }

    private static List<SerializableColorData> ReadColorList(Stream stream, string source)
    {
        try
        {
            var rawList = ColorListSerializer.ReadObject(stream) as List<SerializableColorData>;
            return Normalize(rawList);
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error(
                $"[ColorDataRepository] Failed to deserialize color data ({source}): {ex}"
            );
            return new();
        }
    }

    private static List<SerializableColorData> Normalize(List<SerializableColorData> rawList)
    {
        return rawList?
                .Where(entry => entry != null && entry.ActualColor != null)
                .ToList()
            ?? new();
    }

    private static string SerializeColors(IReadOnlyList<SerializableColorData> colors)
    {
        using MemoryStream stream = new();
        ColorListSerializer.WriteObject(stream, colors);
        return Utf8.GetString(stream.ToArray());
    }

    private static void EnsureUserDataDirectory()
    {
        if (!Directory.Exists(UserDataPath))
        {
            Directory.CreateDirectory(UserDataPath);
        }
    }
}
