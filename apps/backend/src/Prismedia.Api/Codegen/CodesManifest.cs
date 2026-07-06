using System.Reflection;
using Prismedia.Application.Settings;
using Prismedia.Contracts.Entities;
using Prismedia.Domain.Entities;

namespace Prismedia.Api.Codegen;

/// <summary>One enum member's stable code.</summary>
/// <param name="Name">PascalCase domain member name.</param>
/// <param name="Code">Stable wire/storage code.</param>
public sealed record CodeEntry(string Name, string Code);

/// <summary>One named constant string.</summary>
/// <param name="Name">PascalCase constant name.</param>
/// <param name="Value">Constant value.</param>
public sealed record ConstantEntry(string Name, string Value);

/// <summary>Rich metadata for an entity kind, used to generate display labels on the frontend.</summary>
/// <param name="Code">Stable kind code.</param>
/// <param name="DisplayName">Singular display name.</param>
/// <param name="GroupLabel">Plural grouping label.</param>
/// <param name="Category">Broad category name.</param>
/// <param name="StorageShape">Filesystem storage shape name.</param>
public sealed record EntityKindManifestEntry(
    string Code,
    string DisplayName,
    string GroupLabel,
    string Category,
    string StorageShape);

/// <summary>
/// Serializable snapshot of every backend code registry. It is the single source the
/// frontend code generator reads from so that TypeScript code constants are derived from
/// the same <see cref="CodeAttribute"/> enums, capability discriminators, provider keys,
/// and setting keys the backend uses — never hand-maintained in parallel.
/// </summary>
/// <param name="Enums">Code-bearing domain enums keyed by enum type name.</param>
/// <param name="EntityKinds">Entity-kind metadata for display-label generation.</param>
/// <param name="CapabilityKinds">Capability discriminator codes.</param>
/// <param name="ExternalIdProviders">Well-known external-id provider keys.</param>
/// <param name="SettingKeys">App setting keys.</param>
/// <param name="ProblemCodes">Machine-readable API problem codes.</param>
public sealed record CodesManifest(
    IReadOnlyDictionary<string, IReadOnlyList<CodeEntry>> Enums,
    IReadOnlyList<EntityKindManifestEntry> EntityKinds,
    IReadOnlyList<string> CapabilityKinds,
    IReadOnlyList<ConstantEntry> ExternalIdProviders,
    IReadOnlyList<ConstantEntry> SettingKeys,
    IReadOnlyList<ConstantEntry> ProblemCodes) {
    /// <summary>Reflects the current backend registries into a fresh manifest.</summary>
    public static CodesManifest Build() => new(
        BuildEnums(),
        BuildEntityKinds(),
        CapabilityPolymorphism.DiscriminatorKinds,
        ReflectConstants(typeof(Contracts.Entities.ExternalIdProviders)),
        ReflectConstants(typeof(AppSettingKeys)),
        ReflectConstants(typeof(Contracts.System.ApiProblemCodes)));

    private static IReadOnlyDictionary<string, IReadOnlyList<CodeEntry>> BuildEnums() {
        var result = new SortedDictionary<string, IReadOnlyList<CodeEntry>>(StringComparer.Ordinal);
        foreach (var enumType in CodeBearingEnums()) {
            var entries = new List<CodeEntry>();
            foreach (var value in Enum.GetValues(enumType)) {
                var name = Enum.GetName(enumType, value)!;
                var code = enumType.GetField(name)!.GetCustomAttribute<CodeAttribute>()!.Code;
                entries.Add(new CodeEntry(name, code));
            }

            result[enumType.Name] = entries;
        }

        return result;
    }

    private static IEnumerable<Type> CodeBearingEnums() =>
        typeof(EntityKind).Assembly.GetTypes()
            .Where(type => type.IsEnum)
            .Where(type => type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Any(field => field.GetCustomAttribute<CodeAttribute>() is not null));

    private static IReadOnlyList<EntityKindManifestEntry> BuildEntityKinds() =>
        EntityKindRegistry.All
            .Select(descriptor => new EntityKindManifestEntry(
                descriptor.Code,
                descriptor.DisplayName,
                descriptor.GroupLabel,
                descriptor.Category.ToString(),
                descriptor.StorageShape.ToString()))
            .ToArray();

    private static IReadOnlyList<ConstantEntry> ReflectConstants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => new ConstantEntry(field.Name, (string)field.GetRawConstantValue()!))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)
            .ToArray();
}
