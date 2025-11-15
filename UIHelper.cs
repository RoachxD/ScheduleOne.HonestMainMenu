using MelonLoader;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppTMPro;
#elif MONO_BUILD
using TMPro;
#endif

namespace HonestMainMenu;

public static class UIHelper
{
    public static void SetText(this GameObject gameObject, string text)
    {
        if (gameObject == null)
            return;

        TextMeshProUGUI tmpText = gameObject.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpText == null)
            return;

        tmpText.text = text;
    }

    public static bool TryFindChild(
        Transform parent,
        string childPath,
        out Transform child,
        string failureMessage,
        bool logWarning = false
    )
    {
        child = parent?.Find(childPath);
        if (child != null)
        {
            return true;
        }

        if (logWarning)
        {
            Melon<Main>.Logger.Warning(failureMessage);
        }
        else
        {
            Melon<Main>.Logger.Error(failureMessage);
        }

        return false;
    }
}
