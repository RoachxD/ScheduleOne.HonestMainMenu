using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

internal static class MenuSetup
{
    public static MenuButtons Build(Transform menuRoot)
    {
        Transform menuButtonsParent = menuRoot.Find(UIConstants.MenuButtonsParentPath);
        if (menuButtonsParent == null)
        {
            Melon<Main>.Logger.Error(
                $"Could not find menu buttons parent object at '{UIConstants.MainMenuObjectName}/{UIConstants.MenuButtonsParentPath}', aborting UI modifications!"
            );
            return null;
        }

        Transform originalContinueButton = menuButtonsParent.Find(
            UIConstants.ContinueObjectNameAndLabel
        );
        if (originalContinueButton == null)
        {
            Melon<Main>.Logger.Error(
                $"Could not find the original '{UIConstants.ContinueObjectNameAndLabel}' button object, aborting UI modifications!"
            );
            return null;
        }

        var menuButtons = new MenuButtons
        {
            ContinueButton = CreateAndConfigureNewContinueButton(originalContinueButton.gameObject),
            LoadGameButton = null
        };
        if (menuButtons.ContinueButton == null)
        {
            Melon<Main>.Logger.Error(
                $"Failed to create and configure the new '{UIConstants.ContinueObjectNameAndLabel}' button, aborting UI modifications!"
            );
            return null;
        }

        menuButtons.LoadGameButton = ConfigureLoadGameButton(originalContinueButton.gameObject);
        if (menuButtons.LoadGameButton == null)
        {
            Melon<Main>.Logger.Error(
                $"Failed to configure the '{UIConstants.LoadGameObjectName}' button, aborting UI modifications!"
            );
            return null;
        }

        menuButtons.ContinueButton.interactable = false;
        menuButtons.LoadGameButton.interactable = false;
        PositionButtonsAndOffsetSiblings(menuButtons);
        return menuButtons;
    }

    private static Button ConfigureLoadGameButton(GameObject buttonObject)
    {
        var buttonComponent = buttonObject.GetComponent<Button>();
        if (buttonComponent == null)
        {
            return null;
        }

        buttonObject.name = UIConstants.LoadGameObjectName;
        buttonObject.SetText(UIConstants.LoadGameLabel);
        return buttonComponent;
    }

    private static Button CreateAndConfigureNewContinueButton(GameObject buttonObject)
    {
        GameObject newContinueButtonObject = Object.Instantiate(
            buttonObject,
            buttonObject.transform.parent
        );
        newContinueButtonObject.name = UIConstants.ContinueObjectNameAndLabel;
        newContinueButtonObject.SetText(UIConstants.ContinueObjectNameAndLabel);

        var newContinueButton = newContinueButtonObject.GetComponent<Button>();
        if (newContinueButton == null)
        {
            Object.Destroy(newContinueButtonObject);
            return null;
        }

        newContinueButton.onClick = new Button.ButtonClickedEvent(); // Clear any cloned listeners
        return newContinueButton;
    }

    private static void PositionButtonsAndOffsetSiblings(MenuButtons menuButtons)
    {
        RectTransform loadGameRect =
            menuButtons.LoadGameButton.gameObject.GetComponent<RectTransform>();
        RectTransform newContinueRect =
            menuButtons.ContinueButton.gameObject.GetComponent<RectTransform>();
        Transform parentTransform = menuButtons.LoadGameButton.gameObject.transform.parent;
        if (loadGameRect == null || newContinueRect == null || parentTransform == null)
        {
            Melon<Main>.Logger.Error(
                "Could not get RectTransforms or parent for positioning. Destroying new button."
            );
            Object.Destroy(menuButtons.ContinueButton.gameObject);
            return;
        }

        // 1. Store the original position and sibling index of the button that will be shifted down.
        Vector2 originalButtonPos = loadGameRect.anchoredPosition;
        int originalButtonSiblingIndex =
            menuButtons.LoadGameButton.gameObject.transform.GetSiblingIndex();

        // 2. Position the new "Continue" button (menuButtons.ContinueButton.gameObject) at this stored original position and index.
        newContinueRect.anchoredPosition = originalButtonPos;
        menuButtons.ContinueButton.gameObject.transform.SetSiblingIndex(originalButtonSiblingIndex);

        // 3. Position the "Load Game" button (menuButtons.LoadGameButton.gameObject) below the new "Continue" button.
        loadGameRect.anchoredPosition = new Vector2(
            newContinueRect.anchoredPosition.x, // Use the new button's X for consistency
            newContinueRect.anchoredPosition.y - newContinueRect.rect.height // Place it one button-height below the new "Continue"
        );
        menuButtons.LoadGameButton.gameObject.transform.SetSiblingIndex(
            originalButtonSiblingIndex + 1
        );

        // 4. Offset subsequent siblings that come AFTER the "Load Game" button (menuButtons.LoadGameButton.gameObject) in the new hierarchy.
        for (int i = originalButtonSiblingIndex + 2; i < parentTransform.childCount; i++)
        {
            Transform sibling = parentTransform.GetChild(i);
            if (sibling.GetComponent<RectTransform>() is RectTransform siblingRect)
            {
                siblingRect.anchoredPosition = new Vector2(
                    siblingRect.anchoredPosition.x,
                    siblingRect.anchoredPosition.y - newContinueRect.rect.height
                );
            }

#if DEBUG
            Melon<Main>.Logger.Msg(
                $"Offset button '{sibling.name}' (index {i}) by -{newContinueRect.rect.height} on Y axis because the '{UIConstants.LoadGameLabel}' button was shifted down."
            );
#endif
        }
        Melon<Main>.Logger.Msg(
            $"Positioned new '{UIConstants.ContinueObjectNameAndLabel}' at original spot, shifted '{UIConstants.LoadGameObjectName}' ({UIConstants.LoadGameLabel}) below it, and offset subsequent buttons."
        );
    }
}
