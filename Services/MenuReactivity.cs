using System;
using System.Collections;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
#if IL2CPP_BUILD
using Il2CppScheduleOne.Networking;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI.MainMenu;
#elif MONO_BUILD
using ScheduleOne.Networking;
using ScheduleOne.Persistence;
using ScheduleOne.UI.MainMenu;
#endif
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

internal static class MenuReactivity
{
    private static UnityAction _onSaveInfoLoaded;
    private const string ContinueErrorTitle = "Error";
    private const string ContinueErrorMessage =
        "An error occurred while trying to continue the last played game. "
        + "Please try loading it manually from the Load Game menu.";

    public static IEnumerator Run(MenuButtons menuButtons)
    {
        yield return new WaitUntil(new Func<bool>(() => LoadManager.Instance != null));
#if DEBUG
        Melon<Main>.Logger.Msg(
            "LoadManager.Instance is available, proceeding with menu reactivity setup.."
        );
#endif
        if (LoadManager.LastPlayedGame != null)
        {
#if DEBUG
            Melon<Main>.Logger.Msg(
                "LastPlayedGame is not null, updating buttons interactable states."
            );
#endif
            UpdateContinueButtonInteractableState(menuButtons);
            yield break;
        }

#if DEBUG
        Melon<Main>.Logger.Msg(
            "LastPlayedGame is null, waiting for LoadManager.OnSaveInfoLoaded event to update buttons."
        );
#endif
        _onSaveInfoLoaded = (UnityAction)(
            () =>
            {
                Melon<Main>.Logger.Msg(
                    "LoadManager.OnSaveInfoLoaded event triggered, updating buttons interactable states."
                );
                UpdateContinueButtonInteractableState(menuButtons);
            }
        );
        LoadManager.Instance.onSaveInfoLoaded.AddListener(_onSaveInfoLoaded);
    }

    public static void Stop()
    {
        if (_onSaveInfoLoaded == null)
        {
            return;
        }

        LoadManager.Instance.onSaveInfoLoaded.RemoveListener(_onSaveInfoLoaded);
        _onSaveInfoLoaded = null;
#if DEBUG
        Melon<Main>.Logger.Msg(
            "Menu reactivity stopped, LoadManager.OnSaveInfoLoaded listener removed."
        );
#endif
    }

    private static void UpdateContinueButtonInteractableState(MenuButtons menuButtons)
    {
        menuButtons.ContinueButton.onClick.AddListener((UnityAction)PerformNewContinueAction);
        menuButtons.ContinueButton.interactable = true;
        menuButtons.LoadGameButton.interactable = true;
    }

    private static void PerformNewContinueAction()
    {
        if (!Lobby.Instance.IsHost)
        {
            MainMenuPopup.Instance.Open(
                "Cannot Continue",
                "You must be the host in order to be able to continue a game.",
                true
            );
            return;
        }

        try
        {
            LoadManager.Instance.StartGame(LoadManager.LastPlayedGame, false, true);
        }
        catch (MissingMethodException mmEx) when (
            mmEx.Message.Contains("StartGame") ||
            (mmEx.StackTrace?.Contains("LoadManager.StartGame") ?? false)
        )
        {
            try
            {
                LoadManager.Instance.StartGame(LoadManager.LastPlayedGame, false);

                Melon<Main>.Logger.Warning(
                    $"Detected missing method exception while trying to continue the last played game: {mmEx}. " +
                    "This is likely due to an outdated game build. Please update to the latest version. " +
                    "Continuing with the older method signature."
                );
            }
            catch (Exception ex)
            {
                ShowContinueFailureDialog(
                    "Fallback StartGame call also failed after MissingMethodException",
                    ex
                );
            }
        }
        catch (Exception ex)
        {
            ShowContinueFailureDialog(
                "An error occurred while trying to continue the last played game",
                ex
            );
        }
    }

    private static void ShowContinueFailureDialog(string context, Exception exception)
    {
        Melon<Main>.Logger.Error($"{context}: {exception}");
        MainMenuPopup.Instance.Open(ContinueErrorTitle, ContinueErrorMessage, true);
    }
}
