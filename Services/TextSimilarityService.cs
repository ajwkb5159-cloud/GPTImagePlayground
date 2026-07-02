using System.Globalization;

namespace ImageGenerator.Services;

internal sealed class TextSimilarityService
{
    public double CalculateSimilarity(string left, string right)
    {
        var leftVector = BuildVector(left);
        var rightVector = BuildVector(right);
        if (leftVector.Count == 0 || rightVector.Count == 0)
            return 0;

        double dot = 0;
        foreach (var item in leftVector)
        {
            if (rightVector.TryGetValue(item.Key, out var rightValue))
                dot += item.Value * rightValue;
        }

        var leftMagnitude = Math.Sqrt(leftVector.Values.Sum(value => value * value));
        var rightMagnitude = Math.Sqrt(rightVector.Values.Sum(value => value * value));
        if (leftMagnitude <= 0 || rightMagnitude <= 0)
            return 0;

        return dot / (leftMagnitude * rightMagnitude);
    }

    private static Dictionary<string, double> BuildVector(string text)
    {
        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var normalized = Normalize(text);
        if (normalized.Length == 0)
            return vector;

        foreach (var token in ExtractWordTokens(normalized))
            Add(vector, token, 1.25);

        foreach (var token in ExtractCharacterNgrams(normalized, 2))
            Add(vector, token, 1);

        foreach (var token in ExtractCharacterNgrams(normalized, 3))
            Add(vector, token, 0.75);

        return vector;
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var normalized = text.Trim().ToLower(CultureInfo.InvariantCulture);
        return string.Concat(normalized.Select(ch =>
            char.IsLetterOrDigit(ch) || IsCjk(ch) ? ch : ' '));
    }

    private static IEnumerable<string> ExtractWordTokens(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1);

    private static IEnumerable<string> ExtractCharacterNgrams(string text, int size)
    {
        var compact = string.Concat(text.Where(ch => !char.IsWhiteSpace(ch)));
        if (compact.Length < size)
            yield break;

        for (var i = 0; i <= compact.Length - size; i++)
            yield return compact.Substring(i, size);
    }

    private static bool IsCjk(char ch) =>
        ch >= '\u4e00' && ch <= '\u9fff';

    private static void Add(Dictionary<string, double> vector, string token, double weight)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        vector[token] = vector.TryGetValue(token, out var current)
            ? current + weight
            : weight;
    }
}
