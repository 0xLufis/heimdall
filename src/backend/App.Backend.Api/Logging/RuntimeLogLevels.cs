using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace App.Backend.Api.Logging;

/// <summary>
/// In-memory configuration source added last to the configuration chain: a value set here overrides
/// appsettings and environment variables until the process restarts. Every change raises a configuration
/// reload, which the LoggerFactory picks up through <c>IOptionsMonitor&lt;LoggerFilterOptions&gt;</c> and
/// applies to all existing loggers — the standard filter rules keep working unchanged.
/// </summary>
public sealed class RuntimeLogLevelSource : IConfigurationSource
{
    public RuntimeLogLevelConfigurationProvider Provider { get; } = new();

    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
}

public sealed class RuntimeLogLevelConfigurationProvider : ConfigurationProvider
{
    private readonly object _gate = new();

    /// <summary>Sets (or with <c>null</c> removes) configuration keys and raises one reload.</summary>
    public void Apply(IEnumerable<KeyValuePair<string, string?>> changes)
    {
        lock (_gate)
        {
            // Copy-on-write: configuration readers on other threads never see a dictionary being mutated.
            var data = new Dictionary<string, string?>(Data, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in changes)
            {
                if (value == null) data.Remove(key);
                else data[key] = value;
            }
            Data = data;
        }
        OnReload();
    }

    public IReadOnlyDictionary<string, string?> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, string?>(Data, StringComparer.OrdinalIgnoreCase);
        }
    }
}

/// <summary>One runtime override, as reported by the admin endpoint.</summary>
public sealed record RuntimeLogLevelOverride(string Provider, string Category, string Level);

/// <summary>
/// Runtime minimum-level switch per category and output (console, file, or both). Changes live in memory
/// only and are gone after a restart. Backed by <see cref="RuntimeLogLevelSource"/>.
/// </summary>
public sealed class RuntimeLogLevelSwitch
{
    /// <summary>Outputs with their own level section; "All" writes the global key and both of these.</summary>
    public static readonly IReadOnlyList<string> ProviderAliases = new[] { "Console", "File" };
    public const string AllProviders = "All";

    private readonly RuntimeLogLevelConfigurationProvider _provider;

    public RuntimeLogLevelSwitch(RuntimeLogLevelSource source)
    {
        _provider = source.Provider;
    }

    /// <summary>
    /// Sets the minimum level of <paramref name="category"/> (a category prefix, <c>Default</c>, or a
    /// pattern with one <c>*</c>) for one output or for all. <paramref name="level"/> <c>null</c> removes the
    /// override and the configured level applies again.
    /// </summary>
    /// <remarks>
    /// "All" also writes the provider-specific keys because .NET's filter rules always prefer a rule of the
    /// provider's own section (<c>Logging:Console:LogLevel:Default</c>, say) over a global rule, however
    /// specific the global category is.
    /// </remarks>
    public void Set(string category, LogLevel? level, string? provider = null)
    {
        var value = level?.ToString();
        _provider.Apply(KeysFor(category, provider).Select(k => new KeyValuePair<string, string?>(k, value)).ToList());
    }

    /// <summary>Removes every runtime override.</summary>
    public void ResetAll()
    {
        _provider.Apply(_provider.Snapshot().Keys.Select(k => new KeyValuePair<string, string?>(k, null)).ToList());
    }

    public IReadOnlyList<RuntimeLogLevelOverride> Overrides =>
        _provider.Snapshot()
            .Select(kv => Parse(kv.Key, kv.Value))
            .Where(o => o != null)
            .Select(o => o!)
            .OrderBy(o => o.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Provider, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Normalizes the provider name; null when it is not one of All, Console, File.</summary>
    public static string? NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider.Equals(AllProviders, StringComparison.OrdinalIgnoreCase)) return AllProviders;
        return ProviderAliases.FirstOrDefault(p => p.Equals(provider.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Category names become configuration keys, so ':' (the section separator) is not allowed.</summary>
    public static bool IsValidCategory(string? category) =>
        !string.IsNullOrWhiteSpace(category) && category.Length <= 256 && !category.Contains(':') &&
        category.Count(c => c == '*') <= 1 && category.All(c => !char.IsControl(c) && !char.IsWhiteSpace(c));

    private static IEnumerable<string> KeysFor(string category, string? provider)
    {
        if (!IsValidCategory(category)) throw new ArgumentException("Invalid logging category.", nameof(category));
        var normalized = NormalizeProvider(provider) ?? throw new ArgumentException($"Unknown provider '{provider}'.", nameof(provider));
        if (normalized == AllProviders)
        {
            yield return $"Logging:LogLevel:{category}";
            foreach (var alias in ProviderAliases) yield return $"Logging:{alias}:LogLevel:{category}";
        }
        else
        {
            yield return $"Logging:{normalized}:LogLevel:{category}";
        }
    }

    private static RuntimeLogLevelOverride? Parse(string key, string? value)
    {
        if (value == null) return null;
        var parts = key.Split(':');
        if (parts.Length == 3 && parts[0] == "Logging" && parts[1] == "LogLevel")
            return new RuntimeLogLevelOverride(AllProviders, parts[2], value);
        if (parts.Length == 4 && parts[0] == "Logging" && parts[2] == "LogLevel")
            return new RuntimeLogLevelOverride(parts[1], parts[3], value);
        return null;
    }
}

/// <summary>
/// The minimum level a provider applies to a category, by the same rule selection as
/// Microsoft.Extensions.Logging (provider-specific rules first, then the longest matching category,
/// the last of equals wins; no rule: <see cref="LoggerFilterOptions.MinLevel"/>).
/// </summary>
public static class EffectiveLogLevel
{
    public static LogLevel For(LoggerFilterOptions options, string providerFullName, string? providerAlias, string category)
    {
        LoggerFilterRule? current = null;
        foreach (var rule in options.Rules)
        {
            if (IsBetter(rule, current, providerFullName, category) ||
                (!string.IsNullOrEmpty(providerAlias) && IsBetter(rule, current, providerAlias, category)))
            {
                current = rule;
            }
        }
        return current?.LogLevel ?? options.MinLevel;
    }

    private static bool IsBetter(LoggerFilterRule rule, LoggerFilterRule? current, string provider, string category)
    {
        if (rule.ProviderName != null && rule.ProviderName != provider) return false;
        var categoryName = rule.CategoryName;
        if (categoryName != null)
        {
            var wildcard = categoryName.IndexOf('*');
            var prefix = wildcard < 0 ? categoryName : categoryName[..wildcard];
            var suffix = wildcard < 0 ? string.Empty : categoryName[(wildcard + 1)..];
            if (!category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !category.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (current?.ProviderName != null)
        {
            if (rule.ProviderName == null) return false;
        }
        else if (rule.ProviderName != null)
        {
            return true;
        }

        if (current?.CategoryName != null)
        {
            if (rule.CategoryName == null) return false;
            if (current.CategoryName.Length > rule.CategoryName.Length) return false;
        }
        return true;
    }
}
