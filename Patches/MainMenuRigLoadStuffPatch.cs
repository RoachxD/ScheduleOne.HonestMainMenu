using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppScheduleOne.AvatarFramework.Customization;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI.MainMenu;
#elif MONO_BUILD
using ScheduleOne.AvatarFramework.Customization;
using ScheduleOne.Persistence;
using ScheduleOne.UI.MainMenu;
#endif
using HonestMainMenu.Services;

namespace HonestMainMenu.Patches;

[HarmonyPatch(typeof(MainMenuRig), "LoadStuff")]
public static class MainMenuRigLoadStuffPatch
{
    private static readonly HashSet<int> LoadedRigs = new();
    private static readonly object LoadedRigsLock = new();

    public static bool Prefix(MainMenuRig __instance)
    {
        try
        {
            if (!TryBeginRigLoad(__instance))
            {
#if DEBUG
                Melon<Main>.Logger.Msg(
                    "[MainMenuRigLoadStuffPatch] Duplicate LoadStuff detected for this rig; skipping."
                );
#endif
                return false;
            }

            bool flag = false;
            if (LoadManager.LastPlayedGame != null)
            {
                // Step 1: Load the base appearance immediately, just like the original game.
                string playerSavePath = Path.Combine(
                    LoadManager.LastPlayedGame.SavePath,
                    "Players",
                    "Player_0"
                );
                string appearanceJsonPath = Path.Combine(playerSavePath, "Appearance.json");
                BasicAvatarSettings basicAvatarSettings =
                    ScriptableObject.CreateInstance<BasicAvatarSettings>();

                if (File.Exists(appearanceJsonPath))
                {
                    string appearanceJsonText = File.ReadAllText(appearanceJsonPath);
                    JsonUtility.FromJsonOverwrite(appearanceJsonText, basicAvatarSettings);
                    __instance.Avatar.LoadAvatarSettings(basicAvatarSettings.GetAvatarSettings());
                    MelonCoroutines.Start(
                        ClothingDataService.ApplyClothingCoroutine(
                            __instance.Avatar,
                            playerSavePath,
                            basicAvatarSettings
                        )
                    );
                    flag = true;
                }
                else
                {
                    Melon<Main>.Logger.Warning(
                        "[MainMenuRigLoadStuffPatch] Appearance.json not found, skipping avatar appearance load."
                    );
                }

                // Step 2: Load cash piles, same as original.
                float num = LoadManager.LastPlayedGame.Networth;
                for (int i = 0; i < __instance.CashPiles.Length; i++)
                {
                    float num2 = Mathf.Clamp(num, 0f, 100000f);
                    __instance.CashPiles[i].SetDisplayedAmount(num2);
                    num -= 100000f;
                    if (num <= 0f)
                        break;
                }

                Melon<Main>.Logger.Msg(
                    "[MainMenuRigLoadStuffPatch] Completed loading Main Menu Rig and started Avatar appearance application.."
                );
            }

            if (!flag)
            {
                __instance.Avatar.gameObject.SetActive(false);
            }
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error(
                $"[MainMenuRigLoadStuffPatch] An error occurred in the Prefix: {ex}"
            );
            return true; // An error occurred, run the original method to be safe.
        }

        return false; // This tells Harmony to SKIP the original method.
    }

    private static bool TryBeginRigLoad(MainMenuRig rig)
    {
        if (rig == null)
        {
            return false;
        }

        int id = rig.GetInstanceID();
        lock (LoadedRigsLock)
        {
            if (LoadedRigs.Contains(id))
            {
                return false;
            }

            LoadedRigs.Add(id);
            return true;
        }
    }

    public static void ResetLoadedRigs()
    {
        lock (LoadedRigsLock)
        {
            LoadedRigs.Clear();
        }
    }
}

#if IL2CPP_BUILD
[HarmonyPatch(typeof(Il2CppScheduleOne.Clothing.ClothingUtility), "Awake")]
public static class ClothingUtilityAwakePatch
{
    [HarmonyPrefix]
    public static void Prefix(Il2CppScheduleOne.Clothing.ClothingUtility __instance)
    {
        try
        {
            var colors = ClothingDataService.LoadColorData();
            if (colors == null || colors.Count == 0)
            {
                return;
            }

            __instance.ColorDataList ??= new Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Clothing.ClothingUtility.ColorData>();
            __instance.ColorDataList.Clear();

            var enumValues = (Il2CppScheduleOne.Clothing.EClothingColor[])Enum.GetValues(typeof(Il2CppScheduleOne.Clothing.EClothingColor));
            var existing = new HashSet<int>();
            foreach (var entry in colors)
            {
                if (entry == null || entry.ActualColor == null)
                {
                    continue;
                }

                int colorType = entry.ColorType;
                existing.Add(colorType);
                var unityColor = entry.ToUnityColor();
                __instance.ColorDataList.Add(
                    new Il2CppScheduleOne.Clothing.ClothingUtility.ColorData
                    {
                        ColorType = (Il2CppScheduleOne.Clothing.EClothingColor)colorType,
                        ActualColor = unityColor,
                        LabelColor = unityColor
                    }
                );
            }

            foreach (var value in enumValues)
            {
                int colorType = (int)value;
                if (existing.Contains(colorType))
                {
                    continue;
                }

                __instance.ColorDataList.Add(
                    new Il2CppScheduleOne.Clothing.ClothingUtility.ColorData
                    {
                        ColorType = value,
                        ActualColor = UnityEngine.Color.white,
                        LabelColor = UnityEngine.Color.white
                    }
                );
            }
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingService] Failed to seed ClothingUtility colors before Awake: {ex}"
            );
        }
    }
}
#endif
