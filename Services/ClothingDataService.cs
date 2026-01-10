using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppScheduleOne;
using Il2CppScheduleOne.AvatarFramework.Customization;
using Il2CppScheduleOne.Clothing;
using ColorDataList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Clothing.ClothingUtility.ColorData>;
using GameAvatar = Il2CppScheduleOne.AvatarFramework.Avatar;
#elif MONO_BUILD
using ScheduleOne;
using ScheduleOne.AvatarFramework.Customization;
using ScheduleOne.Clothing;
using ColorDataList = System.Collections.Generic.List<ScheduleOne.Clothing.ClothingUtility.ColorData>;
using GameAvatar = ScheduleOne.AvatarFramework.Avatar;
#endif
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

/// <summary>
/// Orchestrates clothing color seeding and item application using lean collaborators.
    /// </summary>
    public static class ClothingDataService
    {
        public static IEnumerator ApplyClothingCoroutine(
        GameAvatar avatar,
        string playerSavePath,
        BasicAvatarSettings basicSettings
    )
    {
        if (avatar == null || basicSettings == null || string.IsNullOrEmpty(playerSavePath))
        {
            Melon<Main>.Logger.Warning(
                "[ClothingDataService] Missing data needed for clothing application."
            );
            yield break;
        }

        List<SerializableColorData> colors = ColorDataRepository.Load();
        ClothingUtilitySeeder.EnsureWithColors(colors);

        var applicator = ClothingApplicatorFactory.Create();
        yield return applicator.Apply(avatar, playerSavePath, basicSettings);
    }

    public static void ExtractAndSaveColorData(ColorDataList liveDataList)
    {
        ColorDataRepository.SaveLiveColors(liveDataList);
    }

    public static List<SerializableColorData> LoadColorData()
    {
        return ColorDataRepository.Load();
    }
}
