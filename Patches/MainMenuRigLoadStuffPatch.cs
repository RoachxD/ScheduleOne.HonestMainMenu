using System;
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
    public static bool Prefix(MainMenuRig __instance)
    {
        try
        {
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
}
