using MelonLoader;
using UnityEngine;

namespace HonestMainMenu.Services;

internal static class ContinueScreenSetup
{
    public static void Apply(Transform mainMenuRoot)
    {
        if (
            !UIHelper.TryFindChild(
                mainMenuRoot,
                UIConstants.ContinueObjectNameAndLabel,
                out Transform continueScreen,
                $"Could not find 'MainMenu/Continue' screen object. Aborting screen configuration."
            )
        )
        {
            return;
        }

        // Set the continue screen's text to match the new "Load Game" button label
        continueScreen.gameObject.name = UIConstants.LoadGameObjectName;
#if DEBUG
        Melon<Main>.Logger.Msg(
            $"Renamed 'MainMenu/Continue' screen object to '{continueScreen.name}'."
        );
#endif

        if (
            !UIHelper.TryFindChild(
                continueScreen,
                UIConstants.TitleObjectName,
                out Transform titleTransform,
                $"Could not find '{UIConstants.TitleObjectName}' child GameObject under '{continueScreen.name}'. The screen title text will not be updated."
            )
        )
        {
            return;
        }

        titleTransform.gameObject.SetText(UIConstants.LoadGameLabel);
#if DEBUG
        Melon<Main>.Logger.Msg(
            $"Set text of '{titleTransform.name}' to '{UIConstants.LoadGameLabel}'."
        );
#endif
    }
}
