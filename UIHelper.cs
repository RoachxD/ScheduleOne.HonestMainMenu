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
}
