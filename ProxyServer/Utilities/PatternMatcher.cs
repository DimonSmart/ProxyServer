using System.Text.RegularExpressions;

namespace DimonSmart.ProxyServer.Utilities;

/// <summary>
/// Utility for matching URL patterns with wildcard support
/// </summary>
public static class PatternMatcher
{
    /// <summary>
    /// Checks if a path matches a pattern with wildcard support
    /// * matches any sequence of characters
    /// ? matches any single character
    /// </summary>
    /// <param name="pattern">Pattern to match against</param>
    /// <param name="path">Path to check</param>
    /// <returns>True if path matches pattern</returns>
    public static bool IsMatch(string pattern, string path)
    {
        if (string.IsNullOrEmpty(pattern) || string.IsNullOrEmpty(path))
        {
            return false;
        }

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".")
            + "$";

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }
}
