using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using MelonLoader;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework;
using Il2CppScheduleOne.AvatarFramework.Customization;
using Il2CppScheduleOne.Clothing;
using GameAvatar = Il2CppScheduleOne.AvatarFramework.Avatar;
#elif MONO_BUILD
using ScheduleOne;
using ScheduleOne.AvatarFramework;
using ScheduleOne.AvatarFramework.Customization;
using ScheduleOne.Clothing;
using GameAvatar = ScheduleOne.AvatarFramework.Avatar;
#endif
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

internal interface IClothingApplicator
{
    IEnumerator Apply(GameAvatar avatar, string playerSavePath, BasicAvatarSettings basicSettings);
}

internal static class ClothingApplicatorFactory
{
    internal static IClothingApplicator Create()
    {
#if IL2CPP_BUILD
        return new Il2CppClothingApplicator();
#else
        return new MonoClothingApplicator();
#endif
    }
}

internal abstract class ClothingApplicatorBase : IClothingApplicator
{
    protected static readonly Encoding Utf8 = new UTF8Encoding(false);
    protected static readonly DataContractJsonSerializerSettings SerializerSettings =
        new() { UseSimpleDictionaryFormat = true };
    protected static readonly DataContractJsonSerializer ClothingFileSerializer =
        new(typeof(SerializableClothingFile), SerializerSettings);
    protected static readonly DataContractJsonSerializer ClothingItemSerializer =
        new(typeof(SerializableClothingSaveData), SerializerSettings);

    public abstract IEnumerator Apply(
        GameAvatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    );

    protected static SerializableClothingFile LoadClothingFile(string playerSavePath)
    {
        string clothingJsonPath = Path.Combine(playerSavePath, "Clothing.json");
        if (!File.Exists(clothingJsonPath))
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingApplicator] Clothing file not found at {clothingJsonPath}. Skipping."
            );
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(clothingJsonPath);
            return ClothingFileSerializer.ReadObject(stream) as SerializableClothingFile;
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error(
                $"[ClothingApplicator] Failed to read clothing file at {clothingJsonPath}: {ex}"
            );
            return null;
        }
    }

    protected static SerializableClothingSaveData DeserializeClothingItem(
        string json,
        string sourcePath
    )
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingApplicator] Empty clothing item JSON found in '{sourcePath}', skipping."
            );
            return null;
        }

        try
        {
            byte[] data = Utf8.GetBytes(json);
            using MemoryStream stream = new(data);
            return ClothingItemSerializer.ReadObject(stream) as SerializableClothingSaveData;
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error(
                $"[ClothingApplicator] Failed to deserialize clothing item ({sourcePath}): {ex}"
            );
            return null;
        }
    }
}

#if IL2CPP_BUILD
internal sealed class Il2CppClothingApplicator : ClothingApplicatorBase
{
    public override IEnumerator Apply(
        GameAvatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    )
    {
        yield return null;

        string appearanceJsonPath = Path.Combine(playerSavePath, "Appearance.json");
        if (!File.Exists(appearanceJsonPath))
        {
            Melon<Main>.Logger.Warning(
                "[ClothingApplicator] Appearance.json not found; skipping clothing application."
            );
            yield break;
        }

        SerializableClothingFile clothingFile = LoadClothingFile(playerSavePath);
        if (clothingFile?.Items == null || clothingFile.Items.Count == 0)
        {
            yield break;
        }

        var playerClothing = avatar.GetComponent<Il2CppScheduleOne.PlayerScripts.PlayerClothing>();
        playerClothing ??= avatar.gameObject.AddComponent<Il2CppScheduleOne.PlayerScripts.PlayerClothing>();

        AvatarSettings avatarSettings = UnityEngine.Object.Instantiate(basicSettings.GetAvatarSettings());
        int appliedCount = 0;
        foreach (string itemJson in clothingFile.Items)
        {
            if (!TryBuildInstance(itemJson, playerSavePath, out var instance))
            {
                continue;
            }

            try
            {
                playerClothing.ApplyClothing(avatarSettings, instance);
                appliedCount++;
            }
            catch (Exception ex)
            {
                Melon<Main>.Logger.Warning(
                    $"[ClothingApplicator] Failed to apply clothing instance: {ex}"
                );
            }
        }

        if (appliedCount > 0)
        {
            avatar.LoadAvatarSettings(avatarSettings);
            Melon<Main>.Logger.Msg(
                $"[ClothingApplicator] Applied {appliedCount} clothing items to the main menu Avatar."
            );
        }
        else
        {
            Melon<Main>.Logger.Warning(
                "[ClothingApplicator] No valid clothing items were applied to the main menu Avatar."
            );
        }
    }

    private static bool TryBuildInstance(
        string itemJson,
        string sourcePath,
        out Il2CppScheduleOne.Clothing.ClothingInstance instance
    )
    {
        instance = null;
        SerializableClothingSaveData clothingData = DeserializeClothingItem(itemJson, sourcePath);
        if (clothingData == null)
        {
            return false;
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
            return false;
        }

        if (
            !ClothingDefinitionResolver.TryResolve(
                clothingData,
                out ClothingDefinition definition,
                out EClothingColor resolvedColor
            )
        )
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingApplicator] Clothing item not found in registry: {clothingData.Id} (source: {sourcePath})"
            );
            return false;
        }

        try
        {
            instance = new Il2CppScheduleOne.Clothing.ClothingInstance(
                definition,
                1,
                resolvedColor
            );
            return true;
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingApplicator] Failed to create ClothingInstance for '{clothingData.Id}': {ex}"
            );
            return false;
        }
    }
}
#else
internal sealed class MonoClothingApplicator : ClothingApplicatorBase
{
    public override IEnumerator Apply(
        GameAvatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    )
    {
        SerializableClothingFile clothingFile = LoadClothingFile(playerSavePath);
        if (clothingFile?.Items == null || clothingFile.Items.Count == 0)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingApplicator] Clothing file at {playerSavePath} was empty or missing."
            );
            yield break;
        }

        AvatarSettings avatarSettings = UnityEngine.Object.Instantiate(basicSettings.GetAvatarSettings());
        avatarSettings.BodyLayerSettings.Clear();
        avatarSettings.AccessorySettings.Clear();

        int appliedCount = 0;
        foreach (string itemJson in clothingFile.Items)
        {
            if (TryApplyItem(avatarSettings, itemJson, playerSavePath))
            {
                appliedCount++;
            }
        }

        avatar.LoadAvatarSettings(avatarSettings);
        if (appliedCount > 0)
        {
            Melon<Main>.Logger.Msg(
                $"[ClothingApplicator] Applied {appliedCount} clothing items to the main menu Avatar."
            );
        }
        else
        {
            Melon<Main>.Logger.Warning(
                "[ClothingApplicator] No valid clothing items were applied to the main menu Avatar."
            );
        }
    }

    private static bool TryApplyItem(
        AvatarSettings avatarSettings,
        string itemJson,
        string sourcePath
    )
    {
        SerializableClothingSaveData clothingData = DeserializeClothingItem(itemJson, sourcePath);
        if (clothingData == null)
        {
            return false;
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
            return false;
        }

        if (
            !ClothingDefinitionResolver.TryResolve(
                clothingData,
                out ClothingDefinition definition,
                out EClothingColor resolvedColor
            )
        )
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingApplicator] Clothing item not found in registry: {clothingData.Id} (source: {sourcePath})"
            );
            return false;
        }

        Color finalColor = clothingColor.GetActualColor();
        if (definition.ApplicationType == EClothingApplicationType.BodyLayer)
        {
            avatarSettings.BodyLayerSettings.Add(
                new AvatarSettings.LayerSetting
                {
                    layerPath = definition.ClothingAssetPath,
                    layerTint = finalColor
                }
            );
        }
        else
        {
            avatarSettings.AccessorySettings.Add(
                new AvatarSettings.AccessorySetting
                {
                    path = definition.ClothingAssetPath,
                    color = finalColor
                }
            );
        }

        return true;
    }
}
#endif
