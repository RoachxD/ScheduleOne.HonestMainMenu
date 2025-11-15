using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
#if IL2CPP_BUILD
using Il2CppScheduleOne.UI.Input;
#elif MONO_BUILD
using ScheduleOne.UI.Input;
#endif

namespace HonestMainMenu.Services;

internal static class BackButtonPromptSetup
{
    public static void Apply(Transform menuRoot)
    {
        if (
            !TryGetInputPrompt(
                menuRoot,
                out InputPrompt inputPromptComponent,
                out GameObject inputPromptOwnerObject
            )
        )
        {
            return; // Error logged by TryGetInputPrompt
        }
        bool actionsWereModified = RemoveActionsMatchingBinding(
            inputPromptComponent,
            UIConstants.RmbPromptBindingKey
        );

        if (actionsWereModified)
        {
            RefreshPrompt(inputPromptComponent, inputPromptOwnerObject);
        }
        else
        {
#if DEBUG
            Melon<Main>.Logger.Msg(
                $"No action matching key '{UIConstants.RmbPromptBindingKey}' found in InputPrompt.Actions for '{UIConstants.InputPromptObjectName}'. No changes made to actions list."
            );
#endif
        }
    }

    private static bool TryGetInputPrompt(
        Transform mainMenuRoot,
        out InputPrompt promptComponent,
        out GameObject ownerObject
    )
    {
        promptComponent = null;
        ownerObject = mainMenuRoot.Find(UIConstants.InputPromptObjectName)?.gameObject;
        if (ownerObject == null)
        {
            Melon<Main>.Logger.Warning(
                $"Could not find InputPrompt owner GameObject at '{UIConstants.InputPromptObjectName}'."
            );
            return false;
        }

        promptComponent = ownerObject.GetComponent<InputPrompt>();
        if (promptComponent == null)
        {
            Melon<Main>.Logger.Warning(
                $"Could not find 'ScheduleOne.UI.Input.InputPrompt' component on '{UIConstants.InputPromptObjectName}'. Make sure the type name is correct."
            );
            return false;
        }
        return true;
    }

    private static bool RemoveActionsMatchingBinding(
        InputPrompt promptComponent,
        string bindingKeyToRemove
    )
    {
#if IL2CPP_BUILD
        var actions = promptComponent.Actions;
        bool removed = false;
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            var actionRef = actions[i];
            if (ShouldRemoveAction(actionRef, bindingKeyToRemove))
            {
                actions.RemoveAt(i);
                removed = true;
            }
        }
        return removed;
#elif MONO_BUILD
        return promptComponent.Actions.RemoveAll(
                actionRef => ShouldRemoveAction(actionRef, bindingKeyToRemove)
            ) > 0;
#endif
    }

    private static bool ShouldRemoveAction(InputActionReference actionRef, string bindingKeyToRemove)
    {
        if (actionRef?.action == null)
        {
            return false;
        }

        actionRef.action.GetBindingDisplayString(0, out _, out string bindingKey, 0);
#if DEBUG
        Melon<Main>.Logger.Msg(
            $"Processing action with binding key: '{bindingKey}' for action name: '{actionRef.action.name}'"
        );
#endif

        bool shouldRemove =
            bindingKeyToRemove.Equals(bindingKey, System.StringComparison.OrdinalIgnoreCase);
#if DEBUG
        if (shouldRemove)
        {
            Melon<Main>.Logger.Msg(
                $"Action with binding key '{bindingKey}' matches '{bindingKeyToRemove}'. It will be removed."
            );
        }
#endif
        return shouldRemove;
    }

    private static void RefreshPrompt(InputPrompt promptComponent, GameObject promptOwnerObject)
    {
#if DEBUG
        Melon<Main>.Logger.Msg(
            $"Updated 'Actions' list for InputPrompt on '{UIConstants.InputPromptObjectName}'. New count: {promptComponent.Actions.Count}"
        );
        foreach (var keptActionRef in promptComponent.Actions)
        {
            if (keptActionRef != null && keptActionRef.action != null)
            {
                keptActionRef.action.GetBindingDisplayString(
                    0,
                    out _,
                    out string keptBindingKey,
                    0
                );
                Melon<Main>.Logger.Msg($"  Remaining action binding key: '{keptBindingKey}'");
            }
        }
#endif
        if (promptOwnerObject.activeInHierarchy)
        {
            promptOwnerObject.SetActive(false);
            promptOwnerObject.SetActive(true);
#if DEBUG
            Melon<Main>.Logger.Msg(
                $"Toggled active state of '{UIConstants.InputPromptObjectName}' to refresh its InputPrompt."
            );
#endif
        }
        else
        {
#if DEBUG
            Melon<Main>.Logger.Msg(
                $"'{UIConstants.InputPromptObjectName}' is not active. Changes to InputPrompt.Actions will apply when it's enabled."
            );
#endif
        }
    }
}
