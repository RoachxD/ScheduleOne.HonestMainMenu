using System;
using System.Collections;
using System.Linq;
using MelonLoader;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppScheduleOne.Networking;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.UI.MainMenu;
#elif MONO_BUILD
using ScheduleOne.Networking;
using ScheduleOne.Persistence;
using ScheduleOne.UI.MainMenu;
#endif

namespace HonestMainMenu.Services;

/// <summary>
/// Handles auto-starting a specific save game, bypassing the main menu.
/// </summary>
internal static class AutoStartService
{
    private static bool _autoStartAttempted;
    private static bool _autoStartSucceeded;

    /// <summary>
    /// Whether an auto-start has already been attempted this session.
    /// </summary>
    public static bool HasAttemptedAutoStart => _autoStartAttempted;

    /// <summary>
    /// Whether auto-start succeeded (game is loading).
    /// </summary>
    public static bool AutoStartSucceeded => _autoStartSucceeded;

    /// <summary>
    /// Resets the auto-start state. Call when returning to menu from a game session.
    /// </summary>
    public static void Reset()
    {
        _autoStartAttempted = false;
        _autoStartSucceeded = false;
    }

    /// <summary>
    /// Attempts to auto-start the configured save. Returns a coroutine that yields
    /// until the attempt is complete.
    /// </summary>
    public static IEnumerator TryAutoStart()
    {
        if (_autoStartAttempted)
        {
            yield break;
        }

        if (!ModConfig.IsAutoStartEnabled)
        {
            yield break;
        }

        _autoStartAttempted = true;
        string targetSaveName = ModConfig.AutoStartSaveName;
        Melon<Main>.Logger.Msg($"Auto-start enabled for save folder: '{targetSaveName}'");

        // Wait for LoadManager to be available
        yield return new WaitUntil(new Func<bool>(() => LoadManager.Instance != null));

        // Check if saves are already loaded
        var targetSave = FindSaveByName(targetSaveName);
        if (targetSave != null)
        {
            yield return StartGameWithSave(targetSave, targetSaveName);
            yield break;
        }

        // Saves might not be loaded yet - poll periodically while also listening for the event
        bool savesLoaded = false;
        void OnSavesLoaded() => savesLoaded = true;

#if IL2CPP_BUILD
        var listener = (UnityEngine.Events.UnityAction)OnSavesLoaded;
#elif MONO_BUILD
        var listener = (UnityEngine.Events.UnityAction)OnSavesLoaded;
#endif

        LoadManager.Instance.onSaveInfoLoaded.AddListener(listener);

        // Wait up to 10 seconds for saves to load, polling every 0.5 seconds
        float timeout = 10f;
        float elapsed = 0f;
        float pollInterval = 0.5f;
        float lastPoll = 0f;

        while (!savesLoaded && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;

            // Periodically check if saves became available
            if (elapsed - lastPoll >= pollInterval)
            {
                lastPoll = elapsed;
                targetSave = FindSaveByName(targetSaveName);
                if (targetSave != null)
                {
                    LoadManager.Instance.onSaveInfoLoaded.RemoveListener(listener);
                    yield return StartGameWithSave(targetSave, targetSaveName);
                    yield break;
                }
            }
        }

        LoadManager.Instance.onSaveInfoLoaded.RemoveListener(listener);

        if (!savesLoaded)
        {
            Melon<Main>.Logger.Warning(
                $"Timed out waiting for saves. Auto-start aborted for '{targetSaveName}'."
            );
            yield break;
        }

        // Try to find the save again after event fired
        targetSave = FindSaveByName(targetSaveName);
        if (targetSave == null)
        {
            Melon<Main>.Logger.Warning(
                $"Save folder '{targetSaveName}' not found. Check your save folder names. Falling back to main menu."
            );
            yield break;
        }

        yield return StartGameWithSave(targetSave, targetSaveName);
    }

    private static SaveInfo FindSaveByName(string saveName)
    {
        // SaveGames is a static property on LoadManager
        if (LoadManager.SaveGames == null)
        {
            return null;
        }

        foreach (var save in LoadManager.SaveGames)
        {
            if (save?.SavePath == null)
            {
                continue;
            }

            // Extract the save name from the path (the folder name is the save name)
            string pathSaveName = System.IO.Path.GetFileName(save.SavePath.TrimEnd(
                System.IO.Path.DirectorySeparatorChar,
                System.IO.Path.AltDirectorySeparatorChar
            ));

            if (!string.IsNullOrEmpty(pathSaveName) &&
                pathSaveName.Equals(saveName, StringComparison.OrdinalIgnoreCase))
            {
                return save;
            }
        }

        return null;
    }

    private static IEnumerator StartGameWithSave(SaveInfo save, string saveName)
    {
        // Wait for Lobby to be ready
        yield return new WaitUntil(new Func<bool>(() => Lobby.Instance != null));

        if (!Lobby.Instance.IsHost)
        {
            Melon<Main>.Logger.Warning(
                "Cannot auto-start: not the host. Falling back to main menu."
            );
            yield break;
        }

        Melon<Main>.Logger.Msg($"Auto-starting save '{saveName}'...");

        try
        {
            LoadManager.Instance.StartGame(save, false, true);
            _autoStartSucceeded = true;
            Melon<Main>.Logger.Msg($"Auto-start initiated successfully for '{saveName}'.");
        }
        catch (MissingMethodException mmEx) when (
            mmEx.Message.Contains("StartGame") ||
            (mmEx.StackTrace?.Contains("LoadManager.StartGame") ?? false))
        {
            try
            {
                LoadManager.Instance.StartGame(save, false);
                _autoStartSucceeded = true;
                Melon<Main>.Logger.Warning(
                    $"Auto-start using legacy method signature for '{saveName}'."
                );
            }
            catch (Exception ex)
            {
                Melon<Main>.Logger.Error($"Auto-start failed (fallback): {ex}");
            }
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Error($"Auto-start failed: {ex}");
        }
    }
}
