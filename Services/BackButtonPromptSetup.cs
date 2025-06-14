using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        List<InputActionReference> filteredActions = GetFilteredInputActions(
            inputPromptComponent.Actions,
            UIConstants.RmbPromptBindingKey,
            out bool actionsWereModified
        );

        if (actionsWereModified)
        {
            ApplyActionsToPrompt(inputPromptComponent, filteredActions, inputPromptOwnerObject);
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

    private static List<InputActionReference> GetFilteredInputActions(
#if IL2CPP_BUILD
        Il2CppSystem.Collections.Generic.List<InputActionReference> currentActions,
#elif MONO_BUILD
        System.Collections.Generic.List<InputActionReference> currentActions,
#endif
        string bindingKeyToRemove,
        out bool actionsModified
    )
    {
        actionsModified = false;
        List<InputActionReference> actionsToKeep = new();

        foreach (InputActionReference actionRef in currentActions)
        {
            if (actionRef == null || actionRef.action == null)
            {
                actionsToKeep.Add(actionRef);
                continue;
            }

            actionRef.action.GetBindingDisplayString(0, out _, out string bindingKey, 0);

#if DEBUG
            Melon<Main>.Logger.Msg(
                $"Processing action with binding key: '{bindingKey}' for action name: '{actionRef.action.name}'"
            );
#endif

            if (bindingKeyToRemove.Equals(bindingKey, System.StringComparison.OrdinalIgnoreCase))
            {
                actionsModified = true;
#if DEBUG
                Melon<Main>.Logger.Msg(
                    $"Action with binding key '{bindingKey}' matches '{bindingKeyToRemove}'. It will be removed."
                );
#endif
            }
            else
            {
                actionsToKeep.Add(actionRef);
            }
        }
        return actionsToKeep; // This is a System.Collections.Generic.List
    }

    [SuppressMessage(
        "csharpsquid",
        "S1172",
        Justification = "Method parameters is used in both IL2CPP and Mono builds."
    )]
    private static void ApplyActionsToPrompt(
        InputPrompt promptComponent,
        List<InputActionReference> newActions,
        GameObject promptOwnerObject
    )
    {
        promptComponent.Actions.Clear();
#if IL2CPP_BUILD
        // newActions is System.Collections.Generic.List<InputActionReference>
        // promptComponent.Actions is Il2CppSystem.Collections.Generic.List<InputActionReference>
        // Adding items one by one is generally safe for interop types.
        foreach (var actionRef in newActions)
        {
            promptComponent.Actions.Add(actionRef);
        }
#elif MONO_BUILD
        // newActions is System.Collections.Generic.List<InputActionReference>
        // promptComponent.Actions is System.Collections.Generic.List<InputActionReference>
        promptComponent.Actions.AddRange(newActions);
#endif

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
