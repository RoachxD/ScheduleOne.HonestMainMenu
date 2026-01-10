using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using MelonLoader;
#if IL2CPP_BUILD
using Il2CppScheduleOne.Clothing;
using PersistenceRegistry = Il2CppScheduleOne.Registry;
#elif MONO_BUILD
using ScheduleOne.Clothing;
#endif
using HonestMainMenu.Models;
using System.Linq;

namespace HonestMainMenu.Services;

/// <summary>
/// Resolves clothing definitions and colors in a single place to avoid duplicated branching.
/// </summary>
internal static class ClothingDefinitionResolver
{
    internal static bool TryResolve(
        SerializableClothingSaveData clothingData,
        out ClothingDefinition definition,
        out EClothingColor resolvedColor
    )
    {
#if IL2CPP_BUILD
        definition = PersistenceRegistry.GetItem(clothingData.Id) as ClothingDefinition;
        resolvedColor = definition?.DefaultColor ?? EClothingColor.White;

        if (definition != null)
        {
            if (clothingData.TryGetValidColor(out var validColor))
            {
                resolvedColor = validColor;
            }
            return true;
        }

        return TryResolveFromRegister(clothingData, out definition, out resolvedColor);
#else
        definition = TryGetDefinitionFromMonoRegistry(clothingData.Id);
        resolvedColor = definition?.DefaultColor ?? EClothingColor.White;
        if (definition != null)
        {
            if (clothingData.TryGetValidColor(out var validColor))
            {
                resolvedColor = validColor;
            }
            return true;
        }

        return TryResolveFromMonoRegister(clothingData, out definition, out resolvedColor);
#endif
    }

#if MONO_BUILD
    private static ClothingDefinition TryGetDefinitionFromMonoRegistry(string id)
    {
        try
        {
            var adapter = MonoRegistryAdapter.GetOrCreate();
            return adapter?.TryGetDefinition(id);
        }
        catch (Exception ex)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingDefinitionResolver] Failed to resolve clothing definition via reflection: {ex}"
            );
            return null;
        }
    }

    private static bool TryResolveFromMonoRegister(
        SerializableClothingSaveData clothingData,
        out ClothingDefinition definition,
        out EClothingColor resolvedColor
    )
    {
        definition = null;
        resolvedColor = clothingData.TryGetValidColor(out var color)
            ? color
            : EClothingColor.White;

        var adapter = MonoRegistryAdapter.GetOrCreate();
        if (adapter == null)
        {
            return false;
        }

        string resolvedId = adapter.ResolveAlias(clothingData.Id);
        object register = adapter.FindRegister(resolvedId);
        if (register == null)
        {
#if DEBUG
            adapter.LogState(resolvedId);
#endif
            return false;
        }

        ClothingDefinition registerDefinition = MonoRegistryAdapter.GetFieldValue<ClothingDefinition>(
            register,
            "Definition",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
        );
        if (registerDefinition != null)
        {
            definition = registerDefinition;
            return true;
        }

        string normalizedPath = NormalizeRegistryAssetPath(
            MonoRegistryAdapter.GetFieldValue<string>(
                register,
                "AssetPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            )
        );
        if (string.IsNullOrEmpty(normalizedPath))
        {
            return false;
        }

        definition = Resources.Load<ClothingDefinition>(normalizedPath);
        if (definition != null)
        {
            return true;
        }

        if (
            TryMapItemDefinition(normalizedPath, out var avatarPath, out var appType)
            && ResourceExists(avatarPath)
        )
        {
            definition = ScriptableObject.CreateInstance<ClothingDefinition>();
            definition.ClothingAssetPath = avatarPath;
            definition.ApplicationType = appType;
            definition.Colorable = true;
            return true;
        }

        return false;
    }

    private sealed class MonoRegistryAdapter
    {
        private static MonoRegistryAdapter _cached;

        private readonly Dictionary<string, object> _registers;
        private readonly Dictionary<string, string> _aliases;

        private MonoRegistryAdapter(Dictionary<string, object> registers, Dictionary<string, string> aliases)
        {
            _registers = registers;
            _aliases = aliases;
        }

        internal static MonoRegistryAdapter GetOrCreate()
        {
            if (_cached != null)
            {
                return _cached;
            }

            Type registryType = Type.GetType("ScheduleOne.Registry, Assembly-CSharp");
            if (registryType == null)
            {
                return null;
            }

            var instanceField = registryType
                .BaseType?
                .BaseType?
                .GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
            var registryInstance = instanceField?.GetValue(null);
            if (registryInstance == null)
            {
                return null;
            }

            var registers = BuildRegisterSnapshot(registryInstance, registryType);
            var aliases = BuildAliasSnapshot(registryInstance, registryType);

            _cached = new MonoRegistryAdapter(registers, aliases);
            return _cached;
        }

        private static Dictionary<string, object> BuildRegisterSnapshot(
            object registryInstance,
            Type registryType
        )
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var itemDictField = registryType.GetField(
                "ItemDictionary",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            var itemDict = itemDictField?.GetValue(registryInstance);
            if (itemDict is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var reg = entry.Value;
                    string regId = GetFieldValue<string>(
                        reg,
                        "ID",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                    );
                    if (!string.IsNullOrEmpty(regId) && !map.ContainsKey(regId))
                    {
                        map[regId] = reg;
                    }
                }
            }

            return map;
        }

        private static Dictionary<string, string> BuildAliasSnapshot(
            object registryInstance,
            Type registryType
        )
        {
            var aliasField = registryType.GetField(
                "itemIDAliases",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
            if (aliasField?.GetValue(registryInstance) is not IDictionary aliasesDict)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in aliasesDict)
            {
                if (entry.Key is string aliasKey && entry.Value is string actualId)
                {
                    aliases[aliasKey] = actualId;
                }
            }

            return aliases;
        }

        internal ClothingDefinition TryGetDefinition(string id)
        {
            string resolvedId = ResolveAlias(id);
            var register = FindRegister(resolvedId);
            if (register == null)
            {
                return null;
            }

            return GetFieldValue<ClothingDefinition>(
                register,
                "Definition",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            );
        }

        internal string ResolveAlias(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return id;
            }

            return _aliases.TryGetValue(id, out var actualId) ? actualId : id;
        }

        internal object FindRegister(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return _registers.TryGetValue(id, out var reg) ? reg : null;
        }

        internal static T GetFieldValue<T>(object instance, string fieldName, BindingFlags flags)
        {
            if (instance == null || string.IsNullOrEmpty(fieldName))
            {
                return default;
            }

            var field = instance.GetType().GetField(fieldName, flags);
            if (field == null)
            {
                return default;
            }

            object value = field.GetValue(instance);
            return value is T typed ? typed : default;
        }

#if DEBUG
        internal void LogState(string missingId)
        {
            Melon<Main>.Logger.Warning(
                $"[ClothingDefinitionResolver] Mono registry missing '{missingId}'. Collected {_registers.Count} ids."
            );

            if (_registers.Count > 0)
            {
                var sample = string.Join(", ", _registers.Keys.Take(10));
                Melon<Main>.Logger.Warning($"[ClothingDefinitionResolver] Sample ids: {sample}");
            }
        }
#endif
    }
#endif

#if IL2CPP_BUILD
    private static bool TryResolveFromRegister(
        SerializableClothingSaveData clothingData,
        out ClothingDefinition definition,
        out EClothingColor resolvedColor
    )
    {
        definition = null;
        resolvedColor = clothingData.TryGetValidColor(out var color)
            ? color
            : EClothingColor.White;

        var register = FindRegister(clothingData.Id);
        if (register == null)
        {
            return false;
        }

        string resourcePath = NormalizeRegistryAssetPath(register.AssetPath);
        if (string.IsNullOrEmpty(resourcePath))
        {
            return false;
        }

        definition = Resources.Load<ClothingDefinition>(resourcePath);
        if (definition != null)
        {
            return true;
        }

        if (
            TryMapItemDefinition(resourcePath, out var avatarPath, out var applicationType)
            && ResourceExists(avatarPath)
        )
        {
            definition = ScriptableObject.CreateInstance<ClothingDefinition>();
            definition.ClothingAssetPath = avatarPath;
            definition.ApplicationType = applicationType;
            definition.Colorable = true;
            return true;
        }

        return false;
    }

    private static PersistenceRegistry.ItemRegister FindRegister(string id)
    {
        var registry = PersistenceRegistry.Instance;
        if (registry?.ItemDictionary == null || string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (var kvp in registry.ItemDictionary)
        {
            var register = kvp?.Value;
            if (register?.ID == null)
            {
                continue;
            }

            if (register.ID.Equals(id, StringComparison.OrdinalIgnoreCase))
            {
                return register;
            }
        }

        return null;
    }

#endif

    private static string NormalizeRegistryAssetPath(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return null;
        }

        string normalized = assetPath.Replace("\\", "/");
        const string prefix = "Assets/Resources/";
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(prefix.Length);
        }

        const string suffix = ".asset";
        if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.Substring(0, normalized.Length - suffix.Length);
        }

        return normalized.Trim('/');
    }

    private static bool TryMapItemDefinition(
        string normalizedPath,
        out string avatarPath,
        out EClothingApplicationType applicationType
    )
    {
        avatarPath = null;
        applicationType = EClothingApplicationType.Accessory;

        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        string[] segments = normalizedPath.Split(
            new[] { '/' },
            StringSplitOptions.RemoveEmptyEntries
        );
        if (segments.Length < 3 || !segments[0].Equals("Clothing", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (
            segments.Length >= 4
            && segments[1].Equals("Accessories", StringComparison.OrdinalIgnoreCase)
        )
        {
            string category = segments[2];
            string item = segments[3];
            avatarPath = $"Avatar/Accessories/{category}/{item}/{item}";
            applicationType = EClothingApplicationType.Accessory;
            return true;
        }

        string simpleCategory = segments[1];
        string itemName = segments[2];
        if (IsAccessoryCategory(simpleCategory))
        {
            avatarPath = $"Avatar/Accessories/{simpleCategory}/{itemName}/{itemName}";
            applicationType = EClothingApplicationType.Accessory;
            return true;
        }

        applicationType = EClothingApplicationType.BodyLayer;
        avatarPath = $"Avatar/Layers/{simpleCategory}/{itemName}";
        return true;
    }

    private static bool IsAccessoryCategory(string category)
    {
        return category.Equals("Head", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Face", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Feet", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Hands", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Neck", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Back", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Accessory", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResourceExists(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return false;
        }

        var loaded = Resources.Load<UnityEngine.Object>(resourcePath);
        return loaded != null;
    }
}
