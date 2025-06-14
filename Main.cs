using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using HonestMainMenu.Services;

namespace HonestMainMenu;

public class Main : MelonMod
{
    public override void OnInitializeMelon()
    {
#if IL2CPP_BUILD
        string buildType = "IL2CPP";
#elif MONO_BUILD
        string buildType = "Mono";
#else
        string buildType = "Unknown";
#endif
        Melon<Main>.Logger.Msg($"Honest Main Menu ({buildType}) initializing..");

        try
        {
            HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            var patchedMethods = HarmonyInstance
                .GetPatchedMethods()
                .Select(p => $"{p.DeclaringType.FullName}.{p.Name}");
            string patchesInfo = string.Join(", ", patchedMethods);
            Melon<Main>.Logger.Msg("Honest Main Menu initialized successfully!");
            Melon<Main>.Logger.Msg($"Harmony patches successfully applied: {patchesInfo}.");
        }
        catch (System.Exception ex)
        {
            Melon<Main>.Logger.Error($"Failed to apply Harmony patches: {ex.Message}");
            Melon<Main>.Logger.Error(ex);
        }
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        if (!sceneName.Equals(UIConstants.MenuSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            MenuReactivity.Stop();
            return;
        }

        Melon<Main>.Logger.Msg(
            $"Main menu scene ('{UIConstants.MenuSceneName}') loaded. Attempting UI modifications.."
        );

        GameObject menuRootObject = GameObject.Find(UIConstants.MainMenuObjectName);
        if (menuRootObject == null)
        {
            Melon<Main>.Logger.Error(
                $"Could not find main menu root object '{UIConstants.MainMenuObjectName}'. Aborting UI modifications."
            );
            return;
        }

        var menuRoot = menuRootObject.transform;
        BackButtonPromptSetup.Apply(menuRoot);

        var menuButtons = MenuSetup.Build(menuRoot);
        if (menuButtons == null)
        {
            return;
        }

        MelonCoroutines.Start(MenuReactivity.Run(menuButtons));
        ContinueScreenSetup.Apply(menuRoot);

        Melon<Main>.Logger.Msg("Honest Main Menu UI modifications applied successfully.");
    }
}
