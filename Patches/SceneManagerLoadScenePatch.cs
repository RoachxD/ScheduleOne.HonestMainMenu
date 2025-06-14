using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace HonestMainMenu.Patches;

[HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadScene), new[] { typeof(string) })]
public static class SceneManagerLoadScenePatch
{
    private static string _lastSceneName = string.Empty;

    [SuppressMessage(
        "csharpsquid",
        "S1144",
        Justification = "Harmony patch method, used by reflection."
    )]
    static bool Prefix(string sceneName)
    {
        if (
            !string.IsNullOrEmpty(_lastSceneName)
            && (
                _lastSceneName.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase)
                || _lastSceneName.Equals(
                    $"Assets/Scenes/{sceneName}.unity",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            return false;
        }

        _lastSceneName = sceneName;
        return true;
    }
}
