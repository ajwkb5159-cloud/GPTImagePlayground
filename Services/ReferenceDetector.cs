namespace ImageGenerator.Services;

internal enum ReferenceStrength
{
    None,
    Weak,
    Strong
}

internal sealed class ReferenceDetectionResult
{
    public ReferenceStrength Strength { get; init; }
    public bool HasReference => Strength != ReferenceStrength.None;
}

internal class ReferenceDetector
{
    private static readonly string[] StrongPatterns =
    [
        "上一张", "上张", "刚才", "这张", "这个图", "那张", "那幅", "它",
        "基于这", "在此基础", "在这个基础", "把这", "把上", "改成", "修改",
        "this image", "that image", "that one", "the last one", "previous image",
        "based on this", "make it", "edit it"
    ];

    private static readonly string[] WeakPatterns =
    [
        "更亮", "亮一点", "暗一点", "更暗", "加一点", "加些", "去掉", "移除",
        "换成", "调整", "继续", "再", "more", "less", "brighter", "darker",
        "add", "remove", "change", "adjust", "continue"
    ];

    public ReferenceDetectionResult Detect(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return new ReferenceDetectionResult { Strength = ReferenceStrength.None };

        var normalized = prompt.Trim().ToLowerInvariant();
        if (StrongPatterns.Any(pattern => normalized.Contains(pattern.ToLowerInvariant())))
            return new ReferenceDetectionResult { Strength = ReferenceStrength.Strong };

        if (WeakPatterns.Any(pattern => normalized.Contains(pattern.ToLowerInvariant())))
            return new ReferenceDetectionResult { Strength = ReferenceStrength.Weak };

        return new ReferenceDetectionResult { Strength = ReferenceStrength.None };
    }
}
