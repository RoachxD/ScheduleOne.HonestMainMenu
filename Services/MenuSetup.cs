using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

internal static class MenuSetup
{
    public static MenuButtons Build(Transform menuRoot)
    {
        if (
            !UIHelper.TryFindChild(
                menuRoot,
                UIConstants.MenuButtonsParentPath,
                out Transform menuButtonsParent,
                $"Could not find menu buttons parent object at '{UIConstants.MainMenuObjectName}/{UIConstants.MenuButtonsParentPath}', aborting UI modifications!"
            )
        )
        {
            return null;
        }

        if (
            !UIHelper.TryFindChild(
                menuButtonsParent,
                UIConstants.ContinueObjectNameAndLabel,
                out Transform originalContinueButton,
                $"Could not find the original '{UIConstants.ContinueObjectNameAndLabel}' button object, aborting UI modifications!"
            )
        )
        {
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
        if (
            !TryGetRects(
                menuButtons,
                out RectTransform loadGameRect,
                out RectTransform continueRect,
                out Transform parentTransform,
                out int originalIndex
            )
        )
        {
            Object.Destroy(menuButtons.ContinueButton.gameObject);
            return;
        }

        float verticalOffset = continueRect.rect.height;
        Vector2 originalPosition = loadGameRect.anchoredPosition;

        PlaceButton(menuButtons.ContinueButton.gameObject, continueRect, originalPosition, originalIndex);
        PlaceButton(
            menuButtons.LoadGameButton.gameObject,
            loadGameRect,
            originalPosition + new Vector2(0f, -verticalOffset),
            originalIndex + 1
        );

        OffsetRemainingSiblings(parentTransform, originalIndex + 2, verticalOffset);
        Melon<Main>.Logger.Msg(
            $"Positioned new '{UIConstants.ContinueObjectNameAndLabel}' at original spot, shifted '{UIConstants.LoadGameObjectName}' ({UIConstants.LoadGameLabel}) below it, and offset subsequent buttons."
        );
    }

    private static bool TryGetRects(
        MenuButtons menuButtons,
        out RectTransform loadGameRect,
        out RectTransform continueRect,
        out Transform parentTransform,
        out int originalIndex
    )
    {
        loadGameRect = menuButtons.LoadGameButton.gameObject.GetComponent<RectTransform>();
        continueRect = menuButtons.ContinueButton.gameObject.GetComponent<RectTransform>();
        parentTransform = menuButtons.LoadGameButton.gameObject.transform.parent;
        originalIndex = menuButtons.LoadGameButton.gameObject.transform.GetSiblingIndex();
        if (loadGameRect != null && continueRect != null && parentTransform != null)
        {
            return true;
        }

        Melon<Main>.Logger.Error(
            "Could not get RectTransforms or parent for positioning. Destroying new button."
        );
        return false;
    }

    private static void PlaceButton(
        GameObject buttonObject,
        RectTransform rectTransform,
        Vector2 anchoredPosition,
        int siblingIndex
    )
    {
        rectTransform.anchoredPosition = anchoredPosition;
        buttonObject.transform.SetSiblingIndex(siblingIndex);
    }

    private static void OffsetRemainingSiblings(Transform parentTransform, int startIndex, float offset)
    {
        for (int i = startIndex; i < parentTransform.childCount; i++)
        {
            Transform sibling = parentTransform.GetChild(i);
            if (sibling.GetComponent<RectTransform>() is RectTransform siblingRect)
            {
                siblingRect.anchoredPosition = new Vector2(
                    siblingRect.anchoredPosition.x,
                    siblingRect.anchoredPosition.y - offset
                );
            }
        }
    }
}
