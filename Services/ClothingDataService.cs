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
    private const int DefaultClothingBufferSize = 256;
    private static readonly Encoding Utf8Encoding = new UTF8Encoding(false);
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
    private static int? _lastAppliedColorHash;

    public static IEnumerator ApplyClothingCoroutine(
        Avatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    )
    {
#if IL2CPP_BUILD
        yield return ApplyClothingIl2Cpp(avatar, playerSavePath, basicSettings);
#else
        yield return ApplyClothingMono(avatar, playerSavePath, basicSettings);
#endif
    }

#if IL2CPP_BUILD
    private static IEnumerator ApplyClothingIl2Cpp(
        Avatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    )
    {
        EnsureClothingUtility();
        yield return null;

        if (ClothingUtility.Instance == null)
        {
            Melon<Main>.Logger.Error(
                "[ClothingService] Failed to make ClothingUtility available. Aborting colorization."
            );
            yield break;
        }

        string clothingJsonPath = Path.Combine(playerSavePath, "Clothing.json");
        SerializableClothingFile clothingFile = DeserializeClothingFile(clothingJsonPath);
        if (clothingFile?.Items == null || clothingFile.Items.Count == 0)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing file at {clothingJsonPath} did not contain any items. Skipping."
            );
            yield break;
        }

        var playerClothing = avatar.GetComponent<Il2CppScheduleOne.PlayerScripts.PlayerClothing>();
        if (playerClothing == null)
        {
            playerClothing = avatar.gameObject.AddComponent<Il2CppScheduleOne.PlayerScripts.PlayerClothing>();
        }

        AvatarSettings newAvatarSettings = UnityEngine.Object.Instantiate(
            basicSettings.GetAvatarSettings()
        );

        int appliedCount = 0;
        foreach (var itemJson in clothingFile.Items)
        {
            var clothingData = DeserializeClothingItem(itemJson, clothingJsonPath);
            if (clothingData == null)
            {
                continue;
            }

            if (
                !string.Equals(
                    clothingData.DataType,
                    "ClothingData",
                    StringComparison.OrdinalIgnoreCase
                )
                || !clothingData.TryGetValidColor(out var clothingColor)
            )
            {
                continue;
            }

            if (
                !TryResolveClothingDefinition(
                    clothingData,
                    out ClothingDefinition def,
                    out EClothingColor colorEnum
                )
            )
            {
                Melon<Main>.Logger.Warning(
                    $"[ClothingService] Clothing item not found in registry: {clothingData.Id} (source: {clothingJsonPath})"
                );
                continue;
            }

            try
            {
                var instance = new Il2CppScheduleOne.Clothing.ClothingInstance(def, 1, colorEnum);
                playerClothing.ApplyClothing(newAvatarSettings, instance);
                appliedCount++;
            }
            catch (Exception ex)
            {
                Melon<Main>.Logger.Warning(
                    $"[ClothingService] Failed to apply clothing '{clothingData.Id}' via PlayerClothing: {ex.Message}"
                );
            }
        }

        if (appliedCount > 0)
        {
            avatar.LoadAvatarSettings(newAvatarSettings);
            Melon<Main>.Logger.Msg(
                $"[ClothingService] Successfully applied {appliedCount} clothing items to the main menu Avatar."
            );
        }
        else
        {
            Melon<Main>.Logger.Warning(
                "[ClothingService] No valid clothing items were applied to the main menu Avatar."
            );
        }
    }
#else
    private static IEnumerator ApplyClothingMono(
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

        bool appliedAny = PopulateClothingSettings(newAvatarSettings, playerSavePath);
        avatar.LoadAvatarSettings(newAvatarSettings);

        if (appliedAny)
        {
            Melon<Main>.Logger.Msg(
                "[ClothingService] Successfully applied colored clothing to main menu Avatar."
            );
        }
        else
        {
            Melon<Main>.Logger.Warning(
                "[ClothingService] No valid clothing items were applied to the main menu Avatar."
            );
        }
    }
#endif
    private static bool PopulateClothingSettings(
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
            return false;
        }

        SerializableClothingFile clothingFile = DeserializeClothingFile(clothingJsonPath);
        if (clothingFile == null)
        {
            Melon<Main>.Logger.Error(
                $"[ClothingService] Failed to parse clothing file at {clothingJsonPath}. Skipping."
            );
            return false;
        }

        if (clothingFile.Items == null || clothingFile.Items.Count == 0)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing file at {clothingJsonPath} did not contain any items. Skipping."
            );
            return false;
        }

#if DEBUG
        int itemCount = clothingFile.Items?.Count ?? 0;
        Melon<Main>.Logger.Msg(
            $"[ClothingService - DEBUG] Found {itemCount} clothing items in the file."
        );
#endif

        ResetClothingItemCache();
        int appliedCount = 0;
        foreach (string itemJson in clothingFile.Items)
        {
            if (ApplySingleClothingItem(avatarSettings, itemJson, clothingJsonPath))
            {
                appliedCount++;
            }
        }

        if (appliedCount > 0)
        {
            Melon<Main>.Logger.Msg(
                $"[ClothingService] Successfully added {appliedCount} clothing items to the Avatar."
            );
            return true;
        }

        Melon<Main>.Logger.Warning(
            $"[ClothingService] Failed to apply any clothing items from {clothingJsonPath}."
        );
        return false;
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
            return false;
        }

        if (!string.Equals(
                clothingData.DataType,
                "ClothingData",
                StringComparison.OrdinalIgnoreCase
            ))
        {
#if DEBUG
            Melon<Main>.Logger.Msg(
                $"[ClothingService - DEBUG] Ignoring non-clothing entry with DataType='{clothingData.DataType}' from '{sourcePath}'."
            );
#endif
            return false;
        }

        if (!clothingData.TryGetValidColor(out var clothingColor))
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Clothing data failed validation for ID '{clothingData.Id}' in '{sourcePath}'. Payload: {TrimForLog(itemJson)}"
            );
            return false;
        }

#if IL2CPP_BUILD
        var register = FindRegister(clothingData.Id);
#endif

        if (Registry.GetItem(clothingData.Id) is not ClothingDefinition def)
        {
#if IL2CPP_BUILD
            LogRegistryStateForDebug(clothingData.Id, register);
            if (
                TryApplyRegisterFallback(
                    avatarSettings,
                    clothingData,
                    clothingColor.GetActualColor(),
                    register
                )
            )
            {
                return true;
            }
#endif
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

        int colorListHash = ComputeColorListHash(colorList);
        if (
            _lastAppliedColorHash.HasValue
            && _lastAppliedColorHash.Value == colorListHash
            && HasColorData(clothingUtility)
        )
        {
#if DEBUG
            Melon<Main>.Logger.Msg(
                "[ClothingService] ClothingUtility already has up-to-date color data. Skipping repopulation."
            );
#endif
            return;
        }
#if IL2CPP_BUILD
        clothingUtility.ColorDataList ??=
            new Il2CppSystem.Collections.Generic.List<ClothingUtility.ColorData>();
        clothingUtility.ColorDataList.Clear();
        var targetList = clothingUtility.ColorDataList;
#else
        clothingUtility.ColorDataList ??= new List<ClothingUtility.ColorData>();
        clothingUtility.ColorDataList.Clear();
        var targetList = clothingUtility.ColorDataList;
#endif
        foreach (var item in colorList)
        {
            Color actualColor = item.ToUnityColor();
            targetList.Add(
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
        _lastAppliedColorHash = colorListHash;
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

            int liveDataHash = ComputeColorListHash(collectedColors);
            FileInfo fileInfo = File.Exists(ColorDataFilePath)
                ? new FileInfo(ColorDataFilePath)
                : null;
            if (ExternalColorCache.Matches(fileInfo, liveDataHash))
            {
                Melon<Main>.Logger.Msg(
                    "[ClothingDataService] Live color data unchanged; skipping disk write."
                );
                return;
            }

            EnsureUserDataDirectory();
            string simplifiedJson = SerializeColorArray(collectedColors);
            File.WriteAllText(ColorDataFilePath, simplifiedJson);
            FileInfo updatedInfo = new(ColorDataFilePath);
            ExternalColorCache.Update(
                new List<SerializableColorData>(collectedColors),
                updatedInfo,
                liveDataHash
            );

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
            ExternalColorCache.Clear();
            return GetEmbeddedColorData(cloneList: true);
        }

        FileInfo fileInfo = new(ColorDataFilePath);
        if (ExternalColorCache.TryGet(fileInfo, out var cachedColors))
        {
#if DEBUG
            Melon<Main>.Logger.Msg(
                "[ClothingDataService] Reusing cached custom color data (no file changes detected)."
            );
#endif
            return cachedColors;
        }

        Melon<Main>.Logger.Msg(
            $"[ClothingDataService] Loading custom color data from '{ColorDataFilePath}'."
        );

        using FileStream stream = File.OpenRead(ColorDataFilePath);
        var colors = DeserializeColorArray(stream, ColorDataFilePath);
        if (colors == null)
        {
            ExternalColorCache.Clear();
            return new List<SerializableColorData>();
        }

        ExternalColorCache.Update(colors, fileInfo, ComputeColorListHash(colors));
        return colors;
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
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        if (ClothingItemCacheStore.TryGet(json, out SerializableClothingSaveData cachedItem))
        {
            return cachedItem;
        }

        int byteCount = Utf8Encoding.GetByteCount(json);
        byte[] buffer = ByteBufferPool.Rent(Math.Max(byteCount, DefaultClothingBufferSize));
        try
        {
            Utf8Encoding.GetBytes(json, 0, json.Length, buffer, 0);
            using MemoryStream stream = new(buffer, 0, byteCount, writable: false, publiclyVisible: true);
            SerializableClothingSaveData deserializedItem = DeserializePayload<SerializableClothingSaveData>(
                ClothingItemSerializer,
                stream,
                sourceDescription,
                payloadForLog: json
            );
            if (deserializedItem != null)
            {
                ClothingItemCacheStore.Remember(json, deserializedItem);
            }

            return deserializedItem;
        }
        finally
        {
            ByteBufferPool.Return(buffer);
        }
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
        if (stream.TryGetBuffer(out ArraySegment<byte> segment) && segment.Array != null)
        {
            return Utf8Encoding.GetString(segment.Array, segment.Offset, segment.Count);
        }

        return Utf8Encoding.GetString(stream.ToArray());
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

#if IL2CPP_BUILD
    private static bool TryResolveClothingDefinition(
        SerializableClothingSaveData clothingData,
        out ClothingDefinition def,
        out EClothingColor colorEnum
    )
    {
        def = Registry.GetItem(clothingData.Id) as ClothingDefinition;
        colorEnum = def?.DefaultColor ?? EClothingColor.White;

        if (def != null)
        {
            if (clothingData.TryGetValidColor(out var validColor))
            {
                colorEnum = validColor;
            }
            return true;
        }

        var register = FindRegister(clothingData.Id);
        if (register == null)
        {
            return false;
        }

        string resourcePath = NormalizeRegistryAssetPath(register.AssetPath);
        if (!string.IsNullOrEmpty(resourcePath))
        {
            var loaded = Resources.Load<ClothingDefinition>(resourcePath);
            if (loaded != null)
            {
                def = loaded;
                if (clothingData.TryGetValidColor(out var c1))
                {
                    colorEnum = c1;
                }
                return true;
            }
        }

        if (
            !string.IsNullOrEmpty(resourcePath)
            && TryMapItemDefinition(resourcePath, out var avatarPath, out var appType)
            && ResourceExists(avatarPath)
        )
        {
            def = ScriptableObject.CreateInstance<ClothingDefinition>();
            def.ClothingAssetPath = avatarPath;
            def.ApplicationType = appType;
            def.Colorable = true;
            if (clothingData.TryGetValidColor(out var c2))
            {
                colorEnum = c2;
            }
            else
            {
                colorEnum = EClothingColor.White;
            }
            return true;
        }

        return false;
    }

    private static Registry.ItemRegister FindRegister(string id)
    {
        var registry = Registry.Instance;
        if (registry?.ItemDictionary == null || string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var kvp in registry.ItemDictionary)
        {
            var reg = kvp?.Value;
            if (reg == null)
            {
                continue;
            }

            string regId = reg.ID;
            if (string.IsNullOrEmpty(regId))
            {
                continue;
            }

            if (regId.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                return reg;
            }
        }

        return null;
    }

    private static void LogRegistryStateForDebug(string missingId, Registry.ItemRegister register = null)
    {
        try
        {
            var registry = Registry.Instance;
            if (registry == null)
            {
                Melon<Main>.Logger.Warning("[ClothingService - DEBUG] Registry.Instance is null.");
                return;
            }

            var dict = registry.ItemDictionary;
            if (dict == null)
            {
                Melon<Main>.Logger.Warning("[ClothingService - DEBUG] Registry.ItemDictionary is null.");
                return;
            }

            Melon<Main>.Logger.Msg(
                $"[ClothingService - DEBUG] Registry.ItemDictionary count: {dict.Count}"
            );

            var ids = new List<string>();
            Registry.ItemRegister matchingRegister = null;
            foreach (var kvp in dict)
            {
                var reg = kvp.Value;
                string id = reg?.ID;
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                    if (
                        matchingRegister == null
                        && missingId != null
                        && id.Equals(missingId, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        matchingRegister = reg;
                    }
                }
            }

            if (ids.Count > 0)
            {
                Melon<Main>.Logger.Msg(
                    "[ClothingService - DEBUG] Sample registry IDs: "
                    + string.Join(", ", ids.Take(10))
                );

                var matches = ids
                    .Where(id =>
                        id.IndexOf(missingId ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0
                    )
                    .Take(5)
                    .ToArray();
                if (matches.Length > 0)
                {
                    Melon<Main>.Logger.Msg(
                        $"[ClothingService - DEBUG] IDs matching '{missingId}': {string.Join(", ", matches)}"
                    );
                }
            }

            if (matchingRegister != null)
            {
                var def = matchingRegister.Definition;
                Melon<Main>.Logger.Msg(
                    def != null
                        ? $"[ClothingService - DEBUG] Register for '{missingId}' has definition type '{def.GetType().Name}'."
                        : $"[ClothingService - DEBUG] Register for '{missingId}' has NULL definition."
                );
                Melon<Main>.Logger.Msg(
                    $"[ClothingService - DEBUG] Register AssetPath: {matchingRegister.AssetPath ?? "<null>"}"
                );
            }
            else if (register != null)
            {
                Melon<Main>.Logger.Msg(
                    $"[ClothingService - DEBUG] Provided register has definition type '{register.Definition?.GetType().Name ?? "null"}' and AssetPath '{register.AssetPath ?? "<null>"}'."
                );
            }
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error($"[ClothingService - DEBUG] Failed to inspect registry: {ex}");
        }
    }

    private static string NormalizeRegistryAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        string normalized = assetPath.Replace("\\", "/");
        const string prefix = "Assets/Resources/";
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(prefix.Length);
        }

        const string suffix = ".asset";
        if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - suffix.Length);
        }

        return normalized.Trim('/');
    }

    private static bool TryMapItemDefinition(
        string normalizedPath,
        out string avatarPath,
        out EClothingApplicationType applicationType
    )
    {
        avatarPath = null;
        applicationType = EClothingApplicationType.Accessory;

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        string[] segments = normalizedPath.Split(
            new[] { '/' },
            StringSplitOptions.RemoveEmptyEntries
        );
        if (segments.Length < 3 || !segments[0].Equals("Clothing", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Clothing/Accessories/<Category>/<Item>
        if (
            segments.Length >= 4
            && segments[1].Equals("Accessories", StringComparison.OrdinalIgnoreCase)
        )
        {
            string category = segments[2];
            string item = segments[3];
            avatarPath = $"Avatar/Accessories/{category}/{item}/{item}";
            applicationType = EClothingApplicationType.Accessory;
            return true;
        }

        // Clothing/<Category>/<Item>
        string simpleCategory = segments[1];
        string itemName = segments[2];
        if (IsAccessoryCategory(simpleCategory))
        {
            avatarPath = $"Avatar/Accessories/{simpleCategory}/{itemName}/{itemName}";
            applicationType = EClothingApplicationType.Accessory;
            return true;
        }

        applicationType = EClothingApplicationType.BodyLayer;
        avatarPath = $"Avatar/Layers/{simpleCategory}/{itemName}";
        return true;
    }

    private static bool IsAccessoryCategory(string category)
    {
        return category.Equals("Head", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Face", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Feet", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Hands", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Neck", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Back", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Accessory", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResourceExists(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return false;
        }

        var loaded = Resources.Load<UnityEngine.Object>(resourcePath);
        return loaded != null;
    }

    private static bool TryApplyRegisterFallback(
        AvatarSettings avatarSettings,
        SerializableClothingSaveData clothingData,
        Color finalColor,
        Registry.ItemRegister register
    )
    {
        register ??= FindRegister(clothingData.Id);
        if (register == null)
        {
            return false;
        }

        string resourcePath = NormalizeRegistryAssetPath(register.AssetPath);
        if (string.IsNullOrEmpty(resourcePath))
        {
            return false;
        }

        var definition = register.Definition;
        ClothingDefinition clothingDefinition = definition as ClothingDefinition;
        if (clothingDefinition != null)
        {
            ApplyDefinitionToAvatar(avatarSettings, clothingDefinition, finalColor);
            Melon<Main>.Logger.Msg(
                $"[ClothingService] Applied '{clothingData.Id}' via register ClothingDefinition fallback (path={clothingDefinition.ClothingAssetPath}, type={clothingDefinition.ApplicationType})."
            );
            return true;
        }

        ClothingDefinition loadedDef = Resources.Load<ClothingDefinition>(resourcePath);
        if (loadedDef != null)
        {
            ApplyDefinitionToAvatar(avatarSettings, loadedDef, finalColor);
            Melon<Main>.Logger.Msg(
                $"[ClothingService] Applied '{clothingData.Id}' via resource ClothingDefinition (path={loadedDef.ClothingAssetPath}, type={loadedDef.ApplicationType})."
            );
            return true;
        }

        if (
            !TryMapItemDefinition(
                resourcePath,
                out string avatarPath,
                out EClothingApplicationType applicationType
            )
        )
        {
            return false;
        }

        if (!ResourceExists(avatarPath))
        {
            return false;
        }

        if (applicationType == EClothingApplicationType.BodyLayer)
        {
            avatarSettings.BodyLayerSettings.Add(
                new AvatarSettings.LayerSetting
                {
                    layerPath = avatarPath,
                    layerTint = finalColor
                }
            );
        }
        else
        {
            avatarSettings.AccessorySettings.Add(
                new AvatarSettings.AccessorySetting
                {
                    path = avatarPath,
                    color = finalColor
                }
            );
        }

        Melon<Main>.Logger.Msg(
            $"[ClothingService] Applied '{clothingData.Id}' via ItemDefinition fallback (path={avatarPath}, type={applicationType})."
        );
        return true;
    }

    private static void ApplyDefinitionToAvatar(
        AvatarSettings avatarSettings,
        ClothingDefinition def,
        Color finalColor
    )
    {
        if (def == null)
        {
            return;
        }

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
    }
#endif
    private static void ResetClothingItemCache()
    {
        ClothingItemCacheStore.Clear();
    }

    private static void EnsureUserDataDirectory()
    {
        if (!Directory.Exists(UserDataPath))
        {
            Directory.CreateDirectory(UserDataPath);
        }
    }

    private static int ComputeColorListHash(IReadOnlyList<SerializableColorData> colors)
    {
        if (colors == null || colors.Count == 0)
        {
            return 0;
        }

        unchecked
        {
            int hash = 17;
            foreach (var entry in colors)
            {
                if (entry == null)
                {
                    hash = (hash * 31) + 0;
                    continue;
                }

                hash = (hash * 31) + entry.ColorType.GetHashCode();
                SerializableColor actualColor = entry.ActualColor;
                if (actualColor != null)
                {
                    hash = (hash * 31) + actualColor.R.GetHashCode();
                    hash = (hash * 31) + actualColor.G.GetHashCode();
                    hash = (hash * 31) + actualColor.B.GetHashCode();
                    hash = (hash * 31) + actualColor.A.GetHashCode();
                }
            }

            return hash;
        }
    }

    private sealed class CachedColorData
    {
        public CachedColorData(
            List<SerializableColorData> colors,
            DateTime lastWriteTimeUtc,
            long length,
            int hash
        )
        {
            Colors = colors ?? new List<SerializableColorData>();
            LastWriteTimeUtc = lastWriteTimeUtc;
            Length = length;
            Hash = hash;
        }

        public List<SerializableColorData> Colors { get; }
        public DateTime LastWriteTimeUtc { get; }
        public long Length { get; }
        public int Hash { get; }

        public bool MatchesFile(FileInfo fileInfo, int? hashOverride = null)
        {
            if (fileInfo == null)
            {
                return false;
            }

            if (
                fileInfo.LastWriteTimeUtc != LastWriteTimeUtc
                || fileInfo.Length != Length
                || (hashOverride.HasValue && hashOverride.Value != Hash)
            )
            {
                return false;
            }

            return true;
        }
    }

    private static class ClothingItemCacheStore
    {
        private static readonly object Lock = new();
        private static readonly Dictionary<string, SerializableClothingSaveData> Cache =
            new(StringComparer.Ordinal);

        public static void Clear()
        {
            lock (Lock)
            {
                Cache.Clear();
            }
        }

        public static bool TryGet(
            string key,
            out SerializableClothingSaveData clothingSaveData
        )
        {
            lock (Lock)
            {
                return Cache.TryGetValue(key, out clothingSaveData);
            }
        }

        public static void Remember(string key, SerializableClothingSaveData clothingSaveData)
        {
            if (clothingSaveData == null)
            {
                return;
            }

            lock (Lock)
            {
                Cache[key] = clothingSaveData;
            }
        }
    }

    private static class ExternalColorCache
    {
        private static readonly object Lock = new();
        private static CachedColorData _cachedData;

        public static void Clear()
        {
            lock (Lock)
            {
                _cachedData = null;
            }
        }

        public static bool TryGet(FileInfo fileInfo, out List<SerializableColorData> colors)
        {
            colors = null;
            if (fileInfo == null)
            {
                return false;
            }

            lock (Lock)
            {
                if (_cachedData?.MatchesFile(fileInfo) == true)
                {
                    colors = _cachedData.Colors;
                    return true;
                }
            }

            return false;
        }

        public static bool Matches(FileInfo fileInfo, int colorHash)
        {
            if (fileInfo == null)
            {
                return false;
            }

            lock (Lock)
            {
                return _cachedData?.MatchesFile(fileInfo, colorHash) == true;
            }
        }

        public static void Update(
            List<SerializableColorData> colors,
            FileInfo fileInfo,
            int colorHash
        )
        {
            if (colors == null || fileInfo == null)
            {
                return;
            }

            List<SerializableColorData> cachedCopy =
                colors is List<SerializableColorData> existingList
                    ? existingList
                    : new List<SerializableColorData>(colors);

            lock (Lock)
            {
                _cachedData = new CachedColorData(
                    cachedCopy,
                    fileInfo.LastWriteTimeUtc,
                    fileInfo.Length,
                    colorHash
                );
            }
        }
    }

    private static class ByteBufferPool
    {
        [ThreadStatic]
        private static byte[] _cachedBuffer;

        public static byte[] Rent(int minimumSize)
        {
            byte[] buffer = _cachedBuffer;
            if (buffer == null || buffer.Length < minimumSize)
            {
                buffer = new byte[Math.Max(minimumSize, DefaultClothingBufferSize)];
            }

            _cachedBuffer = null;
            return buffer;
        }

        public static void Return(byte[] buffer)
        {
            if (buffer == null)
            {
                return;
            }

            _cachedBuffer = buffer;
        }
    }
}
