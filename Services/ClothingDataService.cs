using System;
using System.Collections;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;
using MelonLoader;
using MelonLoader.Utils;

#if IL2CPP_BUILD
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.AvatarFramework.Customization;
using Il2CppScheduleOne.Clothing;
#elif MONO_BUILD
using ScheduleOne;
using ScheduleOne.AvatarFramework;
using ScheduleOne.AvatarFramework.Customization;
using ScheduleOne.Clothing;
#endif
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

public static class ClothingDataService
{
    private static string UserDataPath =>
        Path.Combine(MelonEnvironment.MelonBaseDirectory, "Mods", "HonestMainMenu");
    private static string ColorDataFilePath => Path.Combine(UserDataPath, "ColorData.json");
    private static readonly DataContractJsonSerializerSettings SerializerSettings =
        new() { UseSimpleDictionaryFormat = true };
    private static readonly DataContractJsonSerializer ClothingFileSerializer =
        new(typeof(SerializableClothingFile), SerializerSettings);
    private static readonly DataContractJsonSerializer ClothingItemSerializer =
        new(typeof(SerializableClothingSaveData), SerializerSettings);
    private static readonly DataContractJsonSerializer ColorListSerializer =
        new(typeof(List<SerializableColorData>), SerializerSettings);
    private static readonly object EmbeddedColorDataLock = new();
    private static List<SerializableColorData> _embeddedColorDataCache;

    public static IEnumerator ApplyClothingCoroutine(
        Avatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    )
    {
        EnsureClothingUtility();
        yield return null; // Wait a frame to ensure utility is ready if it was just created.

        if (ClothingUtility.Instance == null)
        {
            Melon<Main>.Logger.Error(
                "[ClothingService] Failed to make ClothingUtility available. Aborting colorization."
            );
            yield break;
        }

        AvatarSettings newAvatarSettings = UnityEngine.Object.Instantiate(
            basicSettings.GetAvatarSettings()
        );
        newAvatarSettings.BodyLayerSettings.Clear();
        newAvatarSettings.AccessorySettings.Clear();

        PopulateClothingSettings(newAvatarSettings, playerSavePath);
        avatar.LoadAvatarSettings(newAvatarSettings);

        Melon<Main>.Logger.Msg(
            "[ClothingService] Successfully applied colored clothing to main menu Avatar."
        );
    }

    private static void PopulateClothingSettings(
        AvatarSettings avatarSettings,
        string playerSavePath
    )
    {
        string clothingJsonPath = Path.Combine(playerSavePath, "Clothing.json");
        if (!File.Exists(clothingJsonPath))
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing file not found at {clothingJsonPath}. Skipping."
            );
            return;
        }

        SerializableClothingFile clothingFile = DeserializeClothingFile(clothingJsonPath);
        if (clothingFile == null)
        {
            Melon<Main>.Logger.Error(
                $"[ClothingService] Failed to parse clothing file at {clothingJsonPath}. Skipping."
            );
            return;
        }

        if (clothingFile.Items == null || clothingFile.Items.Count == 0)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing file at {clothingJsonPath} did not contain any items. Skipping."
            );
            return;
        }

#if DEBUG
        int itemCount = clothingFile.Items?.Count ?? 0;
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Found {itemCount} clothing items in the file."
        );
#endif

        int appliedCount = 0;
        foreach (string itemJson in clothingFile.Items)
        {
            if (ApplySingleClothingItem(avatarSettings, itemJson, clothingJsonPath))
            {
                appliedCount++;
            }
        }

        Melon<Main>.Logger.Msg(
            $"[ClothingService] Successfully added {appliedCount} clothing items to the Avatar."
        );
    }

    private static bool ApplySingleClothingItem(
        AvatarSettings avatarSettings,
        string itemJson,
        string sourcePath
    )
    {
#if DEBUG
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Processing clothing item JSON: {itemJson}"
        );
#endif
        if (string.IsNullOrEmpty(itemJson))
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Empty clothing item JSON found in '{sourcePath}', skipping."
            );
            return false;
        }

        SerializableClothingSaveData clothingData = DeserializeClothingItem(itemJson, sourcePath);
        if (clothingData == null)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Invalid clothing data found in '{sourcePath}', skipping item. Payload: {TrimForLog(itemJson)}"
            );
            return false;
        }

        if (!clothingData.TryGetValidColor(out var clothingColor))
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing data failed validation for ID '{clothingData.Id}' in '{sourcePath}'. Payload: {TrimForLog(itemJson)}"
            );
            return false;
        }

        if (Registry.GetItem(clothingData.Id) is not ClothingDefinition def)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing item not found in registry: {clothingData.Id} (source: {sourcePath})"
            );
            return false;
        }

#if DEBUG
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Found Clothing Definition for ID={clothingData.Id} with color index {clothingData.Color}"
        );
#endif

        Color finalColor = clothingColor.GetActualColor();

#if DEBUG
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Resolved color for clothing item ID={clothingData.Id}: {finalColor}"
        );
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Clothing Definition: AssetPath={def.ClothingAssetPath}, ApplicationType={def.ApplicationType}"
        );
#endif
        if (def.ApplicationType == EClothingApplicationType.BodyLayer)
        {
            avatarSettings.BodyLayerSettings.Add(
                new AvatarSettings.LayerSetting
                {
                    layerPath = def.ClothingAssetPath,
                    layerTint = finalColor
                }
            );
        }
        else if (def.ApplicationType == EClothingApplicationType.Accessory)
        {
            avatarSettings.AccessorySettings.Add(
                new AvatarSettings.AccessorySetting
                {
                    path = def.ClothingAssetPath,
                    color = finalColor
                }
            );
        }

#if DEBUG
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Applied clothing item: ID={clothingData.Id}, Type={def.ApplicationType}, Color={finalColor}"
        );
#endif
        return true;
    }

    private static void EnsureClothingUtility()
    {
        ClothingUtility clothingUtility = ClothingUtility.Instance;
        if (clothingUtility != null && HasColorData(clothingUtility))
        {
#if DEBUG
            Melon<Main>.Logger.Msg("[ClothingService] Existing ClothingUtility already has color data. Skipping repopulation.");
#endif
            return;
        }

        if (clothingUtility == null)
        {
            Melon<Main>.Logger.Msg(
                "[ClothingService] ClothingUtility instance not found, creating a new one."
            );

            GameObject clothingObj = new("@Clothing");
            clothingUtility = clothingObj.AddComponent<ClothingUtility>();
        }

        var colorList = LoadColorData();
        if (colorList == null || colorList.Count == 0)
        {
            Melon<Main>.Logger.Error("[ClothingService] No color data found.");
            return;
        }
#if IL2CPP_BUILD
        clothingUtility.ColorDataList =
            new Il2CppSystem.Collections.Generic.List<ClothingUtility.ColorData>();
#else
        clothingUtility.ColorDataList = new List<ClothingUtility.ColorData>();
#endif
        foreach (var item in colorList)
        {
            Color actualColor = item.ToUnityColor();
            clothingUtility.ColorDataList.Add(
                new ClothingUtility.ColorData
                {
                    ColorType = item.GetEnumColorType(),
                    ActualColor = actualColor
                }
            );
        }

        Melon<Main>.Logger.Msg(
            $"[ClothingService] Loaded {colorList.Count} colors into ClothingUtility (source: {ColorDataFilePath})."
        );
    }

    public static void ExtractAndSaveColorData(
#if IL2CPP_BUILD
        Il2CppSystem.Collections.Generic.List<ClothingUtility.ColorData> liveDataList
#elif MONO_BUILD
        List<ClothingUtility.ColorData> liveDataList
#endif
    )
    {
        try
        {
            Melon<Main>.Logger.Msg(
                $"[ClothingDataService] Extracting {liveDataList.Count} live clothing colors for persistence."
            );
            var collectedColors = new SerializableColorData[liveDataList.Count];
            int index = 0;
            foreach (var colorData in liveDataList)
            {
                collectedColors[index++] = new SerializableColorData(
                    colorData.ColorType,
                    colorData.ActualColor
                );
            }

            // Before saving, compare with the hardcoded defaults.
            var defaultData = GetEmbeddedColorData(cloneList: false);
            if (!collectedColors.SequenceEqual(defaultData))
            {
                Melon<Main>.Logger.Warning(
                    "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"
                );
                Melon<Main>.Logger.Warning(
                    "WARNING: Live game color data does not match the mod's default data."
                );
                Melon<Main>.Logger.Warning(
                    "This may mean the game has updated. The mod will use the new live data,"
                );
                Melon<Main>.Logger.Warning(
                    "but please consider creating a pull request to update the defaults here:"
                );
                Melon<Main>.Logger.Warning("https://github.com/RoachxD/ScheduleOne.HonestMainMenu");
                Melon<Main>.Logger.Warning(
                    "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"
                );
            }
            else
            {
                Melon<Main>.Logger.Msg(
                    "[ClothingDataService] Live color data matches the embedded defaults."
                );
            }

            string simplifiedJson = SerializeColorArray(collectedColors);
            if (!Directory.Exists(UserDataPath))
            {
                Directory.CreateDirectory(UserDataPath);
            }

            File.WriteAllText(ColorDataFilePath, simplifiedJson);
            Melon<Main>.Logger.Msg(
                $"Refreshed {liveDataList.Count} colors in {ColorDataFilePath}."
            );
        }
        catch (Exception e)
        {
            Melon<Main>.Logger.Error($"Failed to extract and save color data: {e}");
        }
    }

    public static List<SerializableColorData> LoadColorData()
    {
        if (!File.Exists(ColorDataFilePath))
        {
            Melon<Main>.Logger.Msg(
                "[ClothingDataService] Custom color data not found. Loading embedded default colors."
            );
            return GetEmbeddedColorData(cloneList: true);
        }

        Melon<Main>.Logger.Msg(
            $"[ClothingDataService] Loading custom color data from '{ColorDataFilePath}'."
        );
        using FileStream stream = File.OpenRead(ColorDataFilePath);
        return DeserializeColorArray(stream, ColorDataFilePath);
    }

    private static List<SerializableColorData> GetEmbeddedColorData(bool cloneList)
    {
        lock (EmbeddedColorDataLock)
        {
            _embeddedColorDataCache ??= LoadEmbeddedColorDataFromResource();
            if (!cloneList)
            {
                return _embeddedColorDataCache;
            }

            return new List<SerializableColorData>(_embeddedColorDataCache);
        }
    }

    private static List<SerializableColorData> LoadEmbeddedColorDataFromResource()
    {
        var assembly = Assembly.GetExecutingAssembly();
        // The resource name is constructed from the default namespace and the filename.
        var resourceName = "HonestMainMenu.Resources.ColorData.json";

        using Stream stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            Melon<Main>.Logger.Error(
                $"[ClothingDataService] Embedded resource '{resourceName}' not found. Ensure the JSON file is set as an Embedded Resource in the project."
            );
            return new();
        }

        var colorList = DeserializeColorArray(stream, resourceName);
#if DEBUG
        Melon<Main>.Logger.Msg(
            $"[ClothingDataService - DEBUG] Loaded {colorList.Count} colors from embedded resource."
        );
#endif
        return colorList;
    }

    private static SerializableClothingFile DeserializeClothingFile(string sourcePath)
    {
        using FileStream stream = File.OpenRead(sourcePath);
        return DeserializePayload<SerializableClothingFile>(
            ClothingFileSerializer,
            stream,
            sourcePath
        );
    }

    private static SerializableClothingSaveData DeserializeClothingItem(
        string json,
        string sourceDescription
    )
    {
        return DeserializePayload<SerializableClothingSaveData>(
            ClothingItemSerializer,
            json,
            sourceDescription,
            includePayloadInLog: true
        );
    }

    private static List<SerializableColorData> DeserializeColorArray(Stream stream, string source)
    {
        var rawList = DeserializePayload<List<SerializableColorData>>(
            ColorListSerializer,
            stream,
            source
        );
        return NormalizeColorList(rawList);
    }

    private static List<SerializableColorData> DeserializeColorArray(string json, string source)
    {
        var rawList = DeserializePayload<List<SerializableColorData>>(
            ColorListSerializer,
            json,
            source
        );
        return NormalizeColorList(rawList);
    }

    private static List<SerializableColorData> NormalizeColorList(
        List<SerializableColorData> rawList
    )
    {
        return rawList?
                .Where(entry => entry != null && entry.ActualColor != null)
                .ToList()
            ?? new();
    }

    private static string SerializeColorArray(IReadOnlyList<SerializableColorData> colors)
    {
        SerializableColorData[] buffer;
        if (colors is SerializableColorData[] directArray)
        {
            buffer = directArray;
        }
        else
        {
            buffer = new SerializableColorData[colors.Count];
            for (int i = 0; i < colors.Count; i++)
            {
                buffer[i] = colors[i];
            }
        }

        using MemoryStream stream = new();
        ColorListSerializer.WriteObject(stream, buffer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static T DeserializePayload<T>(
        DataContractJsonSerializer serializer,
        Stream stream,
        string sourceDescription,
        string payloadForLog = null
    )
        where T : class
    {
        if (stream == null)
            return null;

        try
        {
            return serializer.ReadObject(stream) as T;
        }
        catch (Exception ex)
        {
            string message =
                $"[ClothingDataService] Failed to deserialize {typeof(T).Name} ({sourceDescription}): {ex}";
            if (!string.IsNullOrEmpty(payloadForLog))
            {
                message = $"{message} Payload: {TrimForLog(payloadForLog)}";
            }

            Melon<Main>.Logger.Error(message);
            return null;
        }
    }

    private static T DeserializePayload<T>(
        DataContractJsonSerializer serializer,
        string json,
        string sourceDescription,
        bool includePayloadInLog = false
    )
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        string payloadForLog = includePayloadInLog ? json : null;
        return DeserializePayload<T>(serializer, stream, sourceDescription, payloadForLog);
    }

    private static string TrimForLog(string value, int maxLength = 160)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= maxLength ? value : $"{value.Substring(0, maxLength)}...";
    }

    private static bool HasColorData(ClothingUtility clothingUtility)
    {
        return clothingUtility?.ColorDataList != null && clothingUtility.ColorDataList.Count > 0;
    }
}
