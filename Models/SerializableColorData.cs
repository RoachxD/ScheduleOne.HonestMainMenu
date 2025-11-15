using System;
using System.Runtime.Serialization;
using UnityEngine;
#if IL2CPP_BUILD
using Il2CppScheduleOne.Clothing;
#elif MONO_BUILD
using ScheduleOne.Clothing;
#endif

namespace HonestMainMenu.Models;

[DataContract]
[Serializable]
public sealed class SerializableColorData : IEquatable<SerializableColorData>
{
    [DataMember(Name = "ColorType")]
    public int ColorType { get; private set; }

    [DataMember(Name = "ActualColor")]
    public SerializableColor ActualColor { get; private set; } = new();

    [NonSerialized]
    private Color? _cachedUnityColor;

    public SerializableColorData() { }

    public SerializableColorData(int colorType, SerializableColor actualColor)
    {
        ColorType = colorType;
        ActualColor = actualColor ?? new SerializableColor();
    }

    public SerializableColorData(int colorType, Color actualColor)
        : this(colorType, new SerializableColor(actualColor))
    {
    }

    public SerializableColorData(EClothingColor colorType, Color actualColor)
        : this((int)colorType, actualColor)
    {
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        ActualColor ??= new SerializableColor();
        _cachedUnityColor = null;
    }

    public EClothingColor GetEnumColorType()
    {
        return (EClothingColor)ColorType;
    }

    public Color ToUnityColor()
    {
        if (!_cachedUnityColor.HasValue)
        {
            _cachedUnityColor = ActualColor?.ToUnityColor() ?? Color.white;
        }

        return _cachedUnityColor.Value;
    }

    public bool Equals(SerializableColorData other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return ColorType == other.ColorType && Equals(ActualColor, other.ActualColor);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((SerializableColorData)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + ColorType.GetHashCode();
            hash = hash * 23 + (ActualColor?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
