using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Zerg.Core;

/// <summary>
/// Reads an object from JSON a person may have edited by hand, so that one
/// value of the wrong shape costs that value and not the whole object: the
/// key it is under is dropped, and the property keeps the default the type
/// gives it. A whole number may be written with a fraction (85.5 is 86) or
/// as a string ("90").
/// </summary>
public static class LenientJson
{
    /// <summary>The options <see cref="Read{T}"/> reads with: these, and
    /// whole numbers read leniently.</summary>
    public static JsonSerializerOptions Options(JsonSerializerOptions options)
    {
        var o = new JsonSerializerOptions(options);
        o.Converters.Add(new LenientInt());
        return o;
    }

    /// <summary>
    /// <paramref name="text"/> as a <typeparamref name="T"/>, or null when it
    /// is not a JSON object at all. <paramref name="dropped"/> is told each
    /// top-level key that had to be left out.
    /// </summary>
    /// <param name="options">From <see cref="Options"/>.</param>
    public static T? Read<T>(string text, JsonSerializerOptions options, Action<string, string>? dropped = null)
        where T : class
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException) { return null; }
        if (root is null) return null;

        // Each failure names where it was; the key it is under goes, and the
        // rest are read again. There are only so many keys to drop.
        for (int tries = root.Count + 1; tries > 0; tries--)
        {
            try
            {
                return root.Deserialize<T>(options);
            }
            catch (JsonException e) when (KeyOf(e.Path) is { } key && root.ContainsKey(key))
            {
                root.Remove(key);
                dropped?.Invoke(key, e.Message);
            }
            catch (JsonException) { return null; }
        }
        return null;
    }

    /// <summary>"$.panelOpacity" or "$['odd key'].x" → the top-level key.</summary>
    static string? KeyOf(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith('$') || path.Length < 2) return null;
        if (path[1] == '.')
        {
            int end = path.IndexOfAny(['.', '['], 2);
            return end < 0 ? path[2..] : path[2..end];
        }
        if (path.StartsWith("$['", StringComparison.Ordinal))
        {
            int end = path.IndexOf("']", 3, StringComparison.Ordinal);
            return end < 0 ? null : path[3..end];
        }
        return null;
    }

    /// <summary>A whole number, however it was written, if it is one.</summary>
    sealed class LenientInt : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            double d;
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var i)) return i;
                d = reader.GetDouble();
            }
            else if (reader.TokenType != JsonTokenType.String
                     || !double.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new JsonException("not a number");
            d = Math.Round(d, MidpointRounding.AwayFromZero);
            if (!double.IsFinite(d) || d < int.MinValue || d > int.MaxValue) throw new JsonException("out of range");
            return (int)d;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value);
    }
}
