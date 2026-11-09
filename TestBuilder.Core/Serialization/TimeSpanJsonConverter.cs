using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestBuilder.Core.Serialization;

/// <summary>
/// Сериализует <see cref="TimeSpan"/> строкой в формате <c>[-][d.]hh:mm:ss[.fffffff]</c>.
/// </summary>
public sealed class TimeSpanJsonConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc />
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Ожидалась строка с длительностью в формате 'hh:mm:ss'.");
        }

        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text) ||
            !TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var value))
        {
            throw new JsonException(
                $"Некорректная длительность '{text}'. Ожидается формат 'hh:mm:ss' или 'd.hh:mm:ss'.");
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("c", CultureInfo.InvariantCulture));
    }
}
