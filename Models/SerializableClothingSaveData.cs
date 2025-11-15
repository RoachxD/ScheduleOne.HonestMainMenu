using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
#if IL2CPP_BUILD
using Il2CppScheduleOne.Clothing;
#elif MONO_BUILD
using ScheduleOne.Clothing;
#endif

namespace HonestMainMenu.Models;

[DataContract]
[Serializable]
public sealed class SerializableClothingSaveData
{
    private const string ClothingDataType = "ClothingData";
    private static readonly Type ColorEnumType = typeof(EClothingColor);
    private static readonly Array EnumValues = Enum.GetValues(ColorEnumType);
    private static readonly EClothingColor DefaultColor =
        EnumValues.Length > 0 ? (EClothingColor)EnumValues.GetValue(0) : default;
    private static readonly HashSet<int> ValidColorValues = BuildValidColorSet();

    [DataMember(Name = "DataType")]
    public string DataType { get; set; } = ClothingDataType;

    [DataMember(Name = "ID")]
    public string Id { get; set; }

    [DataMember(Name = "Color")]
    public int Color { get; set; }

    public bool IsValid => TryGetValidColor(out _);

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        DataType ??= ClothingDataType;
    }

    public bool TryGetValidColor(out EClothingColor clothingColor)
    {
        if (
            !string.Equals(DataType, ClothingDataType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrEmpty(Id)
        )
        {
            clothingColor = DefaultColor;
            return false;
        }

        return TryGetColor(out clothingColor);
    }

    public bool TryGetColor(out EClothingColor clothingColor)
    {
        if (ValidColorValues.Contains(Color))
        {
            clothingColor = (EClothingColor)Color;
            return true;
        }

        clothingColor = DefaultColor;
        return false;
    }

    private static HashSet<int> BuildValidColorSet()
    {
        var set = new HashSet<int>();
        foreach (var value in EnumValues)
        {
            set.Add((int)value);
        }

        return set;
    }
}
