namespace Prismedia.Infrastructure.Plugins;

/// <summary>
/// Resolves plugin credential aliases across stored provider credentials and environment variables.
/// </summary>
public static class PluginCredentialResolver {
    /// <summary>
    /// Returns true when any stored credential key can satisfy the manifest field.
    /// </summary>
    public static bool HasCredentialForField(ISet<string> keys, string providerId, string key) =>
        CredentialKeyAliases(providerId, key).Any(keys.Contains);

    /// <summary>
    /// Finds a non-empty stored credential value using the canonical key or compatibility aliases.
    /// </summary>
    public static bool TryResolveStoredCredential(
        IReadOnlyDictionary<string, string> stored,
        string providerId,
        string key,
        out string value) {
        foreach (var alias in CredentialKeyAliases(providerId, key)) {
            if (stored.TryGetValue(alias, out value!) && !string.IsNullOrWhiteSpace(value)) {
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>
    /// Returns true when an environment variable can satisfy the manifest field.
    /// </summary>
    public static bool HasEnvironmentCredential(string providerId, string key) =>
        !string.IsNullOrWhiteSpace(ResolveEnvironmentCredential(providerId, key));

    /// <summary>
    /// Reads the first matching environment variable for a plugin credential field.
    /// </summary>
    public static string? ResolveEnvironmentCredential(string providerId, string key) {
        foreach (var name in EnvironmentCredentialKeys(providerId, key)) {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> CredentialKeyAliases(string providerId, string key) {
        yield return key;
        yield return key.ToUpperInvariant();
        yield return $"{providerId}_{key}";
        yield return $"{providerId.ToUpperInvariant()}_{key.ToUpperInvariant()}";

        if (key.Equals("apiKey", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("api_key", StringComparison.OrdinalIgnoreCase)) {
            yield return $"{providerId}_API_KEY";
            yield return $"{providerId.ToUpperInvariant()}_API_KEY";
        }
    }

    private static string EnvironmentCredentialKey(string providerId, string key) =>
        $"PRISMEDIA_PLUGIN_{providerId.Replace('-', '_').ToUpperInvariant()}_{key.Replace('-', '_').ToUpperInvariant()}";

    private static IEnumerable<string> EnvironmentCredentialKeys(string providerId, string key) {
        yield return EnvironmentCredentialKey(providerId, key);
        if (key.Equals("apiKey", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("api_key", StringComparison.OrdinalIgnoreCase)) {
            yield return $"PRISMEDIA_PLUGIN_{providerId.Replace('-', '_').ToUpperInvariant()}_API_KEY";
            yield return $"{providerId.Replace('-', '_').ToUpperInvariant()}_API_KEY";
        }
    }
}
