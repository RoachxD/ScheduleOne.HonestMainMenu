using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace HonestMainMenu.Models;

[DataContract]
[Serializable]
public sealed class SerializableClothingFile
{
    [DataMember(Name = "Items")]
    public List<string> Items { get; private set; } = new();

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        Items ??= new List<string>();
    }
}
