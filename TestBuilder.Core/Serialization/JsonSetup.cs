using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using TestBuilder.Core.Blocks;

namespace TestBuilder.Core.Serialization;

/// <summary>
/// Единые настройки JSON для файлов <c>.tbtest</c> и отчётов.
/// </summary>
public static class JsonSetup
{
    /// <summary>
    /// Создаёт настройки без доступа к реестру блоков.
    /// Подходит для сериализации, но не позволяет прочитать блоки: используйте перегрузку с реестром.
    /// </summary>
    /// <returns>Настройки сериализации.</returns>
    public static JsonSerializerOptions CreateDefault()
    {
        return CreateDefault(new BlockRegistry());
    }

    /// <summary>
    /// Создаёт настройки с полиморфной сериализацией блоков через <paramref name="registry"/>.
    /// </summary>
    /// <param name="registry">Реестр фабрик блоков.</param>
    /// <returns>Настройки сериализации.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> равен <see langword="null"/>.</exception>
    public static JsonSerializerOptions CreateDefault(IBlockRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        var payloadOptions = CreatePayloadOptions();
        var resolver = new BlockTypeResolver(registry);
        var options = CreatePayloadOptions();
        options.Converters.Add(new TestBlockJsonConverter(resolver, payloadOptions));
        return options;
    }

    private static JsonSerializerOptions CreatePayloadOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
                new TimeSpanJsonConverter(),
            },
        };

        return options;
    }
}
