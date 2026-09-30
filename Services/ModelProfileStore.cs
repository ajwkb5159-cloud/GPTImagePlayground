using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>
/// Single place that resolves per-model token budgets so the settings dialog, the chat model
/// selector, and the context budget planner all agree on defaults and lookup rules.
/// </summary>
internal static class ModelProfileStore
{
    public const int DefaultMaxContextTokens = 32768;
    public const int DefaultMaxOutputTokens = 4096;
    public const int MinMaxContextTokens = 1024;
    public const int MaxMaxContextTokens = 10_000_000;
    public const int MinMaxOutputTokens = 256;
    public const int MaxMaxOutputTokens = 1_000_000;

    /// <summary>
    /// Values above this are flagged in the settings dialog: image models almost never accept such
    /// a window, and the API — not the app — is what rejects the oversized request.
    /// </summary>
    public const int OverlargeContextWarningTokens = 200_000;

    /// <summary>Returns the profile for the app's active model, or a default one when unconfigured.</summary>
    public static ModelContextProfile Resolve(AppConfig config, string? model) =>
        Resolve(config.ModelProfiles, string.IsNullOrWhiteSpace(model) ? config.Model : model);

    /// <summary>Returns the profile for the model, or a default (detached) profile when unconfigured.</summary>
    public static ModelContextProfile Resolve(IList<ModelContextProfile> profiles, string? model)
    {
        var normalized = (model ?? "").Trim();
        var existing = Find(profiles, normalized);
        return existing ?? CreateDefault(normalized);
    }

    /// <summary>Returns the profile for the app's active model, adding a default one when missing.</summary>
    public static ModelContextProfile Ensure(AppConfig config, string? model) =>
        Ensure(
            config.ModelProfiles,
            string.IsNullOrWhiteSpace(model) ? config.Model : model);

    /// <summary>Returns the profile for the model, adding a default one when missing.</summary>
    public static ModelContextProfile Ensure(IList<ModelContextProfile> profiles, string? model)
    {
        var normalized = (model ?? "").Trim();
        var existing = Find(profiles, normalized);
        if (existing != null)
            return existing;

        var created = CreateDefault(normalized);
        profiles.Add(created);
        return created;
    }

    /// <summary>
    /// Normalizes a profile list: trims names, clamps token values, drops empty duplicates and
    /// sorts by model name. Profiles are never created here; call <see cref="Ensure(AppConfig, string?)"/>
    /// first when the active model has to be present in the list.
    /// </summary>
    public static IReadOnlyList<ModelContextProfile> Ordered(IList<ModelContextProfile> profiles)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = profiles.Count - 1; i >= 0; i--)
        {
            var profile = profiles[i];
            profile.Model = (profile.Model ?? "").Trim();
            Normalize(profile);

            if (profile.Model.Length == 0 || !seen.Add(profile.Model))
                profiles.RemoveAt(i);
        }

        return profiles
            .OrderBy(profile => profile.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Applies model-list metadata as a hint; values typed by the user are never overwritten.</summary>
    public static bool TryApplyApiHint(
        ModelContextProfile profile,
        int? maxContextTokens,
        int? maxOutputTokens)
    {
        if (profile.HasManualTokens)
            return false;

        var changed = false;
        if (maxContextTokens is > 0)
        {
            var context = Clamp(maxContextTokens.Value, MinMaxContextTokens, MaxMaxContextTokens);
            if (context != profile.MaxContextTokens)
            {
                profile.MaxContextTokens = context;
                changed = true;
            }
        }

        if (maxOutputTokens is > 0)
        {
            var output = Clamp(maxOutputTokens.Value, MinMaxOutputTokens, MaxMaxOutputTokens);
            if (output < profile.MaxContextTokens && output != profile.MaxOutputTokens)
            {
                profile.MaxOutputTokens = output;
                changed = true;
            }
        }

        if (changed)
            profile.UpdatedAt = DateTime.Now;

        return changed;
    }

    /// <summary>Keeps token values inside the supported ranges and below the context window.</summary>
    public static void Normalize(ModelContextProfile profile)
    {
        profile.MaxContextTokens = Clamp(
            profile.MaxContextTokens,
            MinMaxContextTokens,
            MaxMaxContextTokens);
        profile.MaxOutputTokens = Clamp(
            profile.MaxOutputTokens,
            MinMaxOutputTokens,
            Math.Max(MinMaxOutputTokens, profile.MaxContextTokens - 1));
    }

    public static ModelContextProfile Clone(ModelContextProfile source) => new()
    {
        Model = source.Model,
        MaxContextTokens = source.MaxContextTokens,
        MaxOutputTokens = source.MaxOutputTokens,
        HasManualTokens = source.HasManualTokens,
        UpdatedAt = source.UpdatedAt,
    };

    private static ModelContextProfile CreateDefault(string model) => new()
    {
        Model = model,
        MaxContextTokens = DefaultMaxContextTokens,
        MaxOutputTokens = DefaultMaxOutputTokens,
    };

    private static ModelContextProfile? Find(IList<ModelContextProfile> profiles, string model) =>
        profiles.FirstOrDefault(
            profile => string.Equals((profile.Model ?? "").Trim(), model, StringComparison.OrdinalIgnoreCase));

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));
}
