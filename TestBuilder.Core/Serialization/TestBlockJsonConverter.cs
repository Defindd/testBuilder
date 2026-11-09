using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TestBuilder.Core.Blocks;

namespace TestBuilder.Core.Serialization;

/// <summary>
/// Полиморфный конвертер <see cref="ITestBlock"/> с дискриминатором <c>$type</c>.
/// Конкретный класс блока определяется через <see cref="BlockTypeResolver"/>.
/// </summary>
public sealed class TestBlockJsonConverter : JsonConverter<ITestBlock>
{
    private readonly BlockTypeResolver _resolver;
    private readonly JsonSerializerOptions _payloadOptions;

    /// <summary>
    /// Создаёт конвертер.
    /// </summary>
    /// <param name="resolver">Резолвер типов блоков.</param>
    /// <param name="payloadOptions">
    /// Настройки сериализации полезной нагрузки блока. Не должны содержать этот конвертер,
    /// иначе возникнет рекурсия.
    /// </param>
    public TestBlockJsonConverter(BlockTypeResolver resolver, JsonSerializerOptions payloadOptions)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _payloadOptions = payloadOptions ?? throw new ArgumentNullException(nameof(payloadOptions));
    }

    /// <inheritdoc />
    public override ITestBlock? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Блок должен быть JSON-объектом.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty("$type", out var discriminatorElement) ||
            discriminatorElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("У блока отсутствует строковое свойство '$type'.");
        }

        var blockType = discriminatorElement.GetString();
        if (string.IsNullOrWhiteSpace(blockType))
        {
            throw new JsonException("Свойство '$type' блока пустое.");
        }

        Type clrType;
        try
        {
            clrType = _resolver.ResolveClrType(blockType);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
        {
            throw new JsonException(
                $"Не удалось создать блок типа '{blockType}'. Проверьте, что фабрика зарегистрирована в IBlockRegistry.",
                ex);
        }

        ITestBlock? block;
        try
        {
            block = JsonSerializer.Deserialize(root.GetRawText(), clrType, _payloadOptions) as ITestBlock;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Не удалось прочитать блок типа '{blockType}': {ex.Message}", ex);
        }

        return block ?? throw new JsonException($"Тип '{clrType.FullName}' не реализует ITestBlock.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ITestBlock value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value.BlockType))
        {
            throw new JsonException($"У блока '{value.GetType().FullName}' пустой BlockType.");
        }

        var payload = JsonSerializer.SerializeToNode(value, value.GetType(), _payloadOptions) as JsonObject
                      ?? throw new JsonException(
                          $"Не удалось сериализовать блок '{value.GetType().FullName}' как JSON-объект.");

        writer.WriteStartObject();
        writer.WriteString("$type", value.BlockType);

        foreach (var property in payload)
        {
            if (string.Equals(property.Key, "blockType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Key, "$type", StringComparison.Ordinal))
            {
                continue;
            }

            writer.WritePropertyName(property.Key);
            if (property.Value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                property.Value.WriteTo(writer, _payloadOptions);
            }
        }

        writer.WriteEndObject();
    }
}
