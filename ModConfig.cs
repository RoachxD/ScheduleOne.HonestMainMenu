using MelonLoader;

namespace HonestMainMenu;

/// <summary>
/// MelonLoader preferences for the Honest Main Menu mod.
/// </summary>
public static class ModConfig
{
    private const string CategoryName = "HonestMainMenu";
    private const string CategoryDisplayName = "Honest Main Menu";

    private static MelonPreferences_Category _category;
    private static MelonPreferences_Entry<string> _autoStartSaveName;

    /// <summary>
    /// The name of the save to auto-start. If empty or whitespace, auto-start is disabled.
    /// </summary>
    public static string AutoStartSaveName => _autoStartSaveName?.Value?.Trim() ?? string.Empty;

    /// <summary>
    /// Whether auto-start is enabled (i.e., a save name is configured).
    /// </summary>
    public static bool IsAutoStartEnabled => !string.IsNullOrWhiteSpace(AutoStartSaveName);

    /// <summary>
    /// Initializes the mod configuration. Call this from OnInitializeMelon.
    /// </summary>
    public static void Initialize()
    {
        _category = MelonPreferences.CreateCategory(CategoryName, CategoryDisplayName);

        _autoStartSaveName = _category.CreateEntry(
            identifier: "AutoStartSaveName",
            default_value: "",
            display_name: "Auto-Start Save Name",
            description: "If set, the game will automatically load this save and skip the main menu. " +
                         "Use the save FOLDER name (e.g., 'SaveGame_1')."
        );

        Melon<Main>.Logger.Msg(
            IsAutoStartEnabled
                ? $"Auto-start enabled for save: '{AutoStartSaveName}'"
                : "Auto-start disabled (no save name configured)."
        );
    }
}
