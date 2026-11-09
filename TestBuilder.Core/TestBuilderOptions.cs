namespace TestBuilder.Core;

/// <summary>
/// Глобальные настройки ядра Test Builder.
/// </summary>
public sealed class TestBuilderOptions
{
    /// <summary>
    /// Базовый URL, который используется <see cref="System.Net.Http.HttpClient"/> по умолчанию.
    /// Если задан, все относительные URL в блоках разрешаются относительно него.
    /// </summary>
    public string? DefaultBaseUrl { get; set; }

    /// <summary>
    /// Таймаут HTTP-запросов по умолчанию.
    /// </summary>
    // DEFAULT: 30 секунд — значение, достаточное для большинства интеграционных сценариев.
    public TimeSpan DefaultHttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Проверяет корректность настроек.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <see cref="DefaultBaseUrl"/> задан, но не является абсолютным HTTP(S)-адресом.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="DefaultHttpTimeout"/> не положительный.
    /// </exception>
    public void Validate()
    {
        if (DefaultHttpTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultHttpTimeout),
                DefaultHttpTimeout,
                "Таймаут HTTP по умолчанию должен быть больше нуля.");
        }

        if (string.IsNullOrWhiteSpace(DefaultBaseUrl))
        {
            return;
        }

        if (!Uri.TryCreate(DefaultBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                $"DefaultBaseUrl должен быть абсолютным HTTP(S)-адресом, получено: '{DefaultBaseUrl}'.",
                nameof(DefaultBaseUrl));
        }
    }
}
