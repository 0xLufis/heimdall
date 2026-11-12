namespace App.Backend.Api.Services;

/// <summary>
/// Relevance scoring for the global search. This is a port of the browser ranking
/// (<c>app/utils/search/searchRanking.ts</c> and <c>fuzzy.ts</c>) so the server endpoint and the
/// client-side fallback order results the same way.
///
/// A query is split into whitespace tokens. A record matches only when every token matches at
/// least one of its fields (AND). Each token takes its best field score, weighted by field
/// position (earlier fields matter more); the record score is the mean over tokens.
/// </summary>
public static class SearchRanker
{
    /// <summary>Exact, prefix, word and substring matches score at or above this; typo matches below.</summary>
    public const double SubstringFloor = 0.8;

    public const int MaxTokens = 8;
    public const int MaxTokenLength = 64;

    private const double FieldDecay = 0.03;
    private const double MinFieldWeight = 0.85;
    private const double TypoThreshold = 0.7;

    /// <summary>Lower-cased whitespace tokens, capped in count and length.</summary>
    public static string[] Tokenize(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<string>();
        return query.ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Length > MaxTokenLength ? t[..MaxTokenLength] : t)
            .Take(MaxTokens)
            .ToArray();
    }

    /// <summary>
    /// Identifiers differ by their digits (OP20 is not OP30, Line2 is not Line9), so only
    /// digit-free tokens of at least three characters may match with a typo.
    /// </summary>
    public static bool IsTypoEligible(string token) => token.Length >= 3 && !token.Any(char.IsDigit);

    /// <summary>Score of a record, 0 when any token is unmatched, 1 for an empty token list.</summary>
    public static double ScoreRecord(IReadOnlyList<string> tokens, IReadOnlyList<string?> fields, bool fuzzy = true)
    {
        if (tokens.Count == 0) return 1;

        double total = 0;
        foreach (var token in tokens)
        {
            double best = 0;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (string.IsNullOrEmpty(field)) continue;
                var weight = Math.Max(MinFieldWeight, 1 - i * FieldDecay);
                var score = ScoreToken(token, field, fuzzy) * weight;
                if (score > best) best = score;
            }
            if (best == 0) return 0;
            total += best;
        }
        return total / tokens.Count;
    }

    /// <summary>Score of one lower-cased token against one field, 0 when the field does not match.</summary>
    public static double ScoreToken(string token, string field, bool fuzzy = true)
    {
        var target = field.ToLowerInvariant();
        if (target.Contains(token, StringComparison.Ordinal))
        {
            return Math.Max(SubstringFloor, Similarity(token, target));
        }

        if (!fuzzy || !IsTypoEligible(token)) return 0;
        var score = Similarity(token, target);
        // Within a field, a typo match always ranks below a literal match.
        return score >= TypoThreshold ? Math.Min(score, SubstringFloor - 0.01) : 0;
    }

    /// <summary>
    /// Graded similarity of a lower-cased query against a lower-cased target:
    /// exact 1.0, prefix 0.95, whole word 0.93, word prefix 0.88, substring 0.8-0.9, otherwise an
    /// edit-distance score (0 when too far apart).
    /// </summary>
    public static double Similarity(string query, string target)
    {
        var q = query.Trim();
        var t = target.Trim();
        if (q.Length == 0 || t.Length == 0) return 0;

        if (q == t) return 1.0;
        if (t.StartsWith(q, StringComparison.Ordinal)) return 0.95;

        var words = SplitWords(t);
        if (words.Any(w => w == q)) return 0.93;
        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal))) return 0.88;

        if (t.Contains(q, StringComparison.Ordinal))
        {
            return 0.8 + (double)q.Length / t.Length * 0.1;
        }

        var maxAllowedDistance = q.Length <= 3 ? 1 : q.Length <= 6 ? 2 : 3;

        // Word-level typo match ("siemns" against "Siemens S7-1500").
        double bestWordScore = 0;
        foreach (var word in words)
        {
            if (word.Length < 3) continue;
            // The distance is at least the length difference: skip the matrix when that alone is too far.
            if (Math.Abs(word.Length - q.Length) > maxAllowedDistance) continue;
            var distance = EditDistance(q, word);
            if (distance > maxAllowedDistance) continue;
            var wordScore = 1.0 - (double)distance / Math.Max(q.Length, word.Length);
            if (wordScore > bestWordScore) bestWordScore = wordScore;
        }
        if (bestWordScore >= TypoThreshold) return Math.Max(0.82, bestWordScore * 0.95);

        if (Math.Abs(t.Length - q.Length) > maxAllowedDistance) return 0;
        var fullDistance = EditDistance(q, t);
        if (fullDistance > maxAllowedDistance) return 0;
        return Math.Clamp(1.0 - (double)fullDistance / Math.Max(q.Length, t.Length), 0, 1);
    }

    /// <summary>Damerau-Levenshtein distance (insert, delete, substitute, adjacent transposition).</summary>
    public static int EditDistance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous2 = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, previous2[j - 2] + cost);
                }
                current[j] = value;
            }
            (previous2, previous, current) = (previous, current, previous2);
        }
        return previous[b.Length];
    }

    /// <summary>Words of a field: split on whitespace, underscore, hyphen, en dash and slash.</summary>
    private static List<string> SplitWords(string text)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var isSeparator = i == text.Length || char.IsWhiteSpace(text[i]) || text[i] is '_' or '-' or '–' or '/';
            if (isSeparator)
            {
                if (start >= 0) words.Add(text[start..i]);
                start = -1;
            }
            else if (start < 0)
            {
                start = i;
            }
        }
        return words;
    }
}
