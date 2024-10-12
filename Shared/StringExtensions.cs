using System.Security.Cryptography;
using System.Text;

namespace Shared;
public static class StringExtensions
{
    public static string? GetFileNameFromValidUrl(this string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        Uri? uri;
        if (Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            var path = uri.LocalPath; // Get the path portion of the URL
            return path.Split('/').LastOrDefault(); // Extract the last segment (filename)
        }

        return null; // Return null for invalid URLs
    }
    public static bool IsValidUrl(this string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        Uri? uri;
        return Uri.TryCreate(url, UriKind.Absolute, out uri);
    }
    public static string? GetFileNameFromUrl(this string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var uri = new Uri(url);
        var path = uri.LocalPath; // Get the path portion of the URL
        return path.Split('/').LastOrDefault(); // Extract the last segment (filename)
    }
    public static bool Equals(this string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
    public static bool EqualsAny(this string? str, IEnumerable<string> items, StringComparison stringComparison = StringComparison.OrdinalIgnoreCase)
    {
        if (str == null || items == null)
            return false;

        return items.Any(item => str.Equals(item, stringComparison));
    }

    public static bool CONTAINS(this string? left, string? right, StringComparison comparisonType = StringComparison.OrdinalIgnoreCase)
    {
        return left != null && right != null && left.IndexOf(right, comparisonType) >= 0;
    }

    public static bool ContainsAny(this string? str, params string[] items)
    {
        return str.ContainsAny(items.ToList());
    }

    public static bool ContainsAny(this string? str, IEnumerable<string> items)
    {
        if (str == null || items == null)
            return false;

        return items.Any(item => str.Contains(item, StringComparison.OrdinalIgnoreCase));
    }

    public static bool EndsWithAny(this string? str, IEnumerable<string> items)
    {
        if (str == null || items == null)
            return false;

        return items.Any(item => str.EndsWith(item, StringComparison.OrdinalIgnoreCase));
    }
    public static string ToMD5(this string input)
    {
        using (var md5 = MD5.Create())
        {
            var encoding = Encoding.ASCII;
            var data = encoding.GetBytes(input);

            Span<byte> hashBytes = stackalloc byte[16];
            md5.TryComputeHash(data, hashBytes, out var written);
            if (written != hashBytes.Length)
                throw new OverflowException();


            Span<char> stringBuffer = stackalloc char[32];
            for (var i = 0; i < hashBytes.Length; i++) hashBytes[i].TryFormat(stringBuffer.Slice(2 * i), out _, "x2");
            return new string(stringBuffer);
        }
    }

    private static readonly char[] TrimChars = [' ', '\t', '\n', '\r'];
    public static string? TrimSelf(this string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        input = new StringBuilder(input).TrimSelf();
        return input;
    }
    public static string? TrimSelf(this StringBuilder input)
    {
        if (input == null)
            return null;
        for (int i = input.Length - 1; i >= 0; i--)
        {
            if (TrimChars.Contains(input[i]))
            {
                input.Remove(i, 1);
            }
        }
        return input.ToString();
    }
    public static string? TrimResult(this string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        char[] trimChars = { ' ', '\t', '\n', '\r' };
        // Create a StringBuilder to build the trimmed string
        var trimmed = new StringBuilder(input.Length);

        foreach (char c in input)
        {
            if (!trimChars.Contains(c))
            {
                trimmed.Append(c);
            }
        }

        return trimmed.ToString();
    }
    public static bool HasText(this string? input)
    {
        return !string.IsNullOrEmpty(input);
    }
    public static bool HasTextWithTrim(this string? input)
    {
        return !string.IsNullOrEmpty(input.TrimSelf());
    }
    public static bool IsNullOrEmptyString(this string? input)
    {
        return input == null || string.IsNullOrEmpty(input.TrimResult());
    }
    public static bool IsNullOrEmptyAndTrimSelf(this string? input)
    {
        return string.IsNullOrEmpty(input.TrimSelf());
    }

    public static string? SubStringTruncate(this string? str, int length = 25, string? truncationSymbol = null)
    {
        //ex: madhusudhan  
        //if length expected is 10, then if more than 10 then add 10
        //                                          if 10-dots length logic
        if (str == null)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(truncationSymbol))
            return str.Length <= length ? str : str[..length];
        else
        {
            if (str.Length <= length) return str;
            else
            {

            }
            return str![..length] + truncationSymbol;
        }
    }
    public static string SubstringSafe(this string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty; // Return empty string for null or empty input
        }

        return text.Length <= maxLength ? text : text.Substring(0, maxLength);
    }

    public static string UrlGetDomain(this string? text, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(text))
        {
            if (!string.IsNullOrEmpty(defaultValue)) return defaultValue;
            return string.Empty; // Return empty string for null or empty input
        }
        var t1 = text.Replace("https://www.", string.Empty, StringComparison.InvariantCultureIgnoreCase);
        if (t1.IsNullOrEmptyString() && !defaultValue.IsNullOrEmptyString()) return defaultValue;
        return t1.Trim();
    }
    public static List<string> CsvToList(this string csvString)
    {//also removes duplicates and all
        if (string.IsNullOrEmpty(csvString))
        {
            return [];
        }

        string[] csvArray = csvString.Split(',');

        csvArray = csvArray.Distinct().Where(s => !string.IsNullOrEmpty(s)).ToArray();

        if (csvArray.Length > 0 && csvArray[csvArray.Length - 1] == "")
        {
            csvArray = csvArray.Take(csvArray.Length - 1).ToArray();
        }

        return csvArray.ToList();
    }

    public static string ToCsv(this IEnumerable<string?>? stringList)
    {
        if (stringList == null || stringList.Count() == 0)
        {
            return ""; // Handle empty list gracefully
        }

        StringBuilder stringBuilder = new StringBuilder();

        foreach (string? item in stringList)
        {
            if (string.IsNullOrEmpty(item)) continue;
            // Enclose item in double quotes if it contains commas or double quotes
            if (item.Contains(",") || item.Contains("\""))
            {
                stringBuilder.Append('\"');
                stringBuilder.Append(item.Replace("\"", "\"\""))
                    .Append('\"');
            }
            else
            {
                stringBuilder.Append(item);
            }

            stringBuilder.Append(",");
        }
        // Remove the trailing comma
        if (stringBuilder.Length > 0)
        {
            stringBuilder.Length--;
        }

        return stringBuilder.ToString();
    }
}
