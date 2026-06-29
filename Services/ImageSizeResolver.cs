namespace ImageGenerator.Services;

internal static class ImageSizeResolver
{
    private const int SizeMultiple = 16;
    private const int MaxEdge = 3840;
    private const double MaxAspectRatio = 3;
    private const int MinPixels = 655_360;
    private const int MaxPixels = 8_294_400;

    private static readonly Dictionary<string, Dictionary<string, string>> CommonSizePresets = new()
    {
        ["1K"] = new()
        {
            ["1:1"] = "1024x1024",
            ["3:2"] = "1536x1024",
            ["2:3"] = "1024x1536",
            ["16:9"] = "1280x720",
            ["9:16"] = "720x1280",
            ["4:3"] = "1024x768",
            ["3:4"] = "768x1024",
            ["21:9"] = "1280x544",
        },
        ["2K"] = new()
        {
            ["1:1"] = "2048x2048",
            ["3:2"] = "2160x1440",
            ["2:3"] = "1440x2160",
            ["16:9"] = "2560x1440",
            ["9:16"] = "1440x2560",
            ["4:3"] = "2048x1536",
            ["3:4"] = "1536x2048",
            ["21:9"] = "2560x1088",
        },
        ["4K"] = new()
        {
            ["1:1"] = "2880x2880",
            ["3:2"] = "3456x2304",
            ["2:3"] = "2304x3456",
            ["16:9"] = "3840x2160",
            ["9:16"] = "2160x3840",
            ["4:3"] = "3200x2400",
            ["3:4"] = "2400x3200",
            ["21:9"] = "3840x1600",
        },
    };

    public static string Resolve(string mode, string tier, string ratio, int customWidth, int customHeight)
    {
        if (string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase))
            return "auto";

        if (string.Equals(mode, "preset", StringComparison.OrdinalIgnoreCase)
            && CommonSizePresets.TryGetValue(tier, out var tierPresets)
            && tierPresets.TryGetValue(ratio, out var presetSize))
        {
            return presetSize;
        }

        if (string.Equals(mode, "preset", StringComparison.OrdinalIgnoreCase)
            && TryParseRatio(ratio, out var ratioWidth, out var ratioHeight)
            && TryCalculateSize(tier, ratioWidth, ratioHeight, out var calculatedSize))
        {
            return calculatedSize;
        }

        var normalized = NormalizeDimensions(customWidth, customHeight);
        return $"{normalized.width}x{normalized.height}";
    }

    private static bool TryCalculateSize(string tier, double ratioWidth, double ratioHeight, out string size)
    {
        var pixelBudget = tier switch
        {
            "2K" => 4_194_304,
            "4K" => MaxPixels,
            _ => 1_572_864,
        };
        var targetRatio = ratioWidth / ratioHeight;
        var bestWidth = 0;
        var bestHeight = 0;
        var bestPixels = 0;

        for (var width = SizeMultiple; width <= MaxEdge; width += SizeMultiple)
        {
            var idealHeight = width / targetRatio;
            var candidates = new[]
            {
                (int)Math.Floor(idealHeight / SizeMultiple) * SizeMultiple,
                (int)Math.Ceiling(idealHeight / SizeMultiple) * SizeMultiple,
            };

            foreach (var height in candidates)
            {
                if (height < SizeMultiple || height > MaxEdge) continue;

                var pixels = width * height;
                if (pixels > pixelBudget || pixels < MinPixels) continue;
                if (Math.Max((double)width / height, (double)height / width) > MaxAspectRatio) continue;

                var ratioError = Math.Abs(((double)width / height) - targetRatio) / targetRatio;
                if (ratioError > 0.01) continue;

                if (pixels > bestPixels)
                {
                    bestPixels = pixels;
                    bestWidth = width;
                    bestHeight = height;
                }
            }
        }

        size = bestPixels > 0 ? $"{bestWidth}x{bestHeight}" : "";
        return bestPixels > 0;
    }

    private static (int width, int height) NormalizeDimensions(int width, int height)
    {
        var normalizedWidth = RoundToMultiple(Math.Max(SizeMultiple, width), SizeMultiple);
        var normalizedHeight = RoundToMultiple(Math.Max(SizeMultiple, height), SizeMultiple);

        for (var i = 0; i < 4; i++)
        {
            var maxEdge = Math.Max(normalizedWidth, normalizedHeight);
            if (maxEdge > MaxEdge)
                ScaleToFit(ref normalizedWidth, ref normalizedHeight, (double)MaxEdge / maxEdge);

            if ((double)normalizedWidth / normalizedHeight > MaxAspectRatio)
                normalizedWidth = FloorToMultiple((int)Math.Round(normalizedHeight * MaxAspectRatio), SizeMultiple);
            else if ((double)normalizedHeight / normalizedWidth > MaxAspectRatio)
                normalizedHeight = FloorToMultiple((int)Math.Round(normalizedWidth * MaxAspectRatio), SizeMultiple);

            var pixels = normalizedWidth * normalizedHeight;
            if (pixels > MaxPixels)
                ScaleToFit(ref normalizedWidth, ref normalizedHeight, Math.Sqrt((double)MaxPixels / pixels));
            else if (pixels < MinPixels)
                ScaleToFill(ref normalizedWidth, ref normalizedHeight, Math.Sqrt((double)MinPixels / pixels));
        }

        return (normalizedWidth, normalizedHeight);
    }

    private static bool TryParseRatio(string ratio, out double width, out double height)
    {
        width = 0;
        height = 0;
        var parts = ratio.Split(':', 'x', 'X');
        return parts.Length == 2
            && double.TryParse(parts[0], out width)
            && double.TryParse(parts[1], out height)
            && width > 0
            && height > 0;
    }

    private static void ScaleToFit(ref int width, ref int height, double scale)
    {
        width = FloorToMultiple((int)Math.Round(width * scale), SizeMultiple);
        height = FloorToMultiple((int)Math.Round(height * scale), SizeMultiple);
    }

    private static void ScaleToFill(ref int width, ref int height, double scale)
    {
        width = CeilToMultiple((int)Math.Round(width * scale), SizeMultiple);
        height = CeilToMultiple((int)Math.Round(height * scale), SizeMultiple);
    }

    private static int RoundToMultiple(int value, int multiple) =>
        Math.Max(multiple, (int)Math.Round((double)value / multiple) * multiple);

    private static int FloorToMultiple(int value, int multiple) =>
        Math.Max(multiple, (int)Math.Floor((double)value / multiple) * multiple);

    private static int CeilToMultiple(int value, int multiple) =>
        Math.Max(multiple, (int)Math.Ceiling((double)value / multiple) * multiple);
}
