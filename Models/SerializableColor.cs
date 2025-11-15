using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace HonestMainMenu.Models;

[DataContract]
[Serializable]
public sealed class SerializableColor : IEquatable<SerializableColor>
{
    [DataMember(Name = "r")]
    public float R { get; private set; }

    [DataMember(Name = "g")]
    public float G { get; private set; }

    [DataMember(Name = "b")]
    public float B { get; private set; }

    [DataMember(Name = "a")]
    public float A
    {
        get => _alpha;
        private set
        {
            _alpha = value;
            _alphaExplicitlySet = true;
        }
    }

    private float _alpha = 1f;
    private bool _alphaExplicitlySet;

    public SerializableColor() { }

    public SerializableColor(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public SerializableColor(Color color)
    {
        R = color.r;
        G = color.g;
        B = color.b;
        A = color.a;
    }

    public Color ToUnityColor()
    {
        return new Color(R, G, B, A);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        if (!_alphaExplicitlySet)
        {
            _alpha = 1f;
        }
    }

    public bool Equals(SerializableColor other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);
    }

    public override bool Equals(object obj)
    {
        if (obj is null)
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((SerializableColor)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + R.GetHashCode();
            hash = hash * 23 + G.GetHashCode();
            hash = hash * 23 + B.GetHashCode();
            hash = hash * 23 + A.GetHashCode();
            return hash;
        }
    }
}
