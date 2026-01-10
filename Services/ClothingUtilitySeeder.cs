using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppScheduleOne.Clothing;
using ColorDataList = Il2CppSystem.Collections.Generic.List<Il2CppScheduleOne.Clothing.ClothingUtility.ColorData>;
#elif MONO_BUILD
using ScheduleOne.Clothing;
using ColorDataList = System.Collections.Generic.List<ScheduleOne.Clothing.ClothingUtility.ColorData>;
#endif
using HonestMainMenu.Models;

namespace HonestMainMenu.Services;

/// <summary>
/// Ensures a ClothingUtility instance exists and is populated with the provided colors.
/// </summary>
internal static class ClothingUtilitySeeder
{
    private static int? _lastHash;

    internal static ClothingUtility EnsureWithColors(IReadOnlyList<SerializableColorData> colors)
    {
        var clothingUtility = ClothingUtility.Instance;
        if (clothingUtility == null)
        {
            Melon<Main>.Logger.Msg(
                "[ClothingUtilitySeeder] ClothingUtility instance not found, creating a new one."
            );
            GameObject clothingObj = new("@Clothing");
            clothingUtility = clothingObj.AddComponent<ClothingUtility>();
        }

        Populate(clothingUtility, colors);
        return clothingUtility;
    }

    internal static void Populate(
        ClothingUtility clothingUtility,
        IReadOnlyList<SerializableColorData> colors
    )
    {
        if (clothingUtility == null)
        {
            Melon<Main>.Logger.Error(
                "[ClothingUtilitySeeder] Cannot populate colors because ClothingUtility is null."
            );
            return;
        }

        if (colors == null || colors.Count == 0)
        {
            Melon<Main>.Logger.Error(
                "[ClothingUtilitySeeder] No color data provided; ClothingUtility will remain unchanged."
            );
            return;
        }

        if (IsAlreadySeeded(clothingUtility, colors))
        {
            return;
        }

        PopulateColorList(clothingUtility, colors);
        _lastHash = ComputeHash(colors);
    }

    private static void PopulateColorList(
        ClothingUtility clothingUtility,
        IReadOnlyList<SerializableColorData> colors
    )
    {
#if IL2CPP_BUILD
        clothingUtility.ColorDataList ??= new ColorDataList();
        var targetList = clothingUtility.ColorDataList;
        targetList.Clear();
#else
        clothingUtility.ColorDataList ??= new ColorDataList();
        var targetList = clothingUtility.ColorDataList;
        targetList.Clear();
#endif
        foreach (var entry in colors)
        {
            if (entry == null)
            {
                continue;
            }

            var unityColor = entry.ToUnityColor();
            targetList.Add(
                new ClothingUtility.ColorData
                {
                    ColorType = entry.GetEnumColorType(),
                    ActualColor = unityColor
                }
            );
        }

#if DEBUG
        Melon<Main>.Logger.Msg(
            $"[ClothingUtilitySeeder] Loaded {colors.Count} colors into ClothingUtility."
        );
#endif
    }

    private static bool IsAlreadySeeded(
        ClothingUtility clothingUtility,
        IReadOnlyList<SerializableColorData> colors
    )
    {
        if (clothingUtility?.ColorDataList == null || colors == null)
        {
            return false;
        }

        if (colors.Count == 0)
        {
            return false;
        }

        int incomingHash = ComputeHash(colors);
        if (_lastHash.HasValue && _lastHash.Value == incomingHash)
        {
            return true;
        }

        if (clothingUtility.ColorDataList.Count != colors.Count)
        {
            return false;
        }

        for (int i = 0; i < colors.Count; i++)
        {
            var existing = clothingUtility.ColorDataList[i];
            var incoming = colors[i];
            if (existing == null || incoming == null)
            {
                return false;
            }

            if (existing.ColorType != incoming.GetEnumColorType())
            {
                return false;
            }

            if (existing.ActualColor != incoming.ToUnityColor())
            {
                return false;
            }
        }

        _lastHash = incomingHash;
        return true;
    }

    private static int ComputeHash(IReadOnlyList<SerializableColorData> colors)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < colors.Count; i++)
            {
                var entry = colors[i];
                hash = (hash * 31) + (entry?.ColorType.GetHashCode() ?? 0);
                var color = entry?.ToUnityColor() ?? Color.white;
                hash = (hash * 31) + color.r.GetHashCode();
                hash = (hash * 31) + color.g.GetHashCode();
                hash = (hash * 31) + color.b.GetHashCode();
                hash = (hash * 31) + color.a.GetHashCode();
            }

            return hash;
        }
    }
}
