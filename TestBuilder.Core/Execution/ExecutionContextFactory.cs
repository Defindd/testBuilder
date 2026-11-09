using Microsoft.Extensions.Logging;
using TestBuilder.Core.Scenarios;
using TestBuilder.Core.Templating;

namespace TestBuilder.Core.Execution;

/// <summary>
/// Создаёт <see cref="ExecutionContext"/> для запуска сценария.
/// </summary>
public sealed class ExecutionContextFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TemplateEngine _templates;

    /// <summary>
    /// Создаёт фабрику контекста.
    /// </summary>
    /// <param name="httpClientFactory">Фабрика HTTP-клиентов.</param>
    /// <param name="loggerFactory">Фабрика логгеров.</param>
    /// <param name="templates">Движок шаблонов.</param>
    public ExecutionContextFactory(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        TemplateEngine templates)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }

    /// <summary>
    /// Создаёт контекст выполнения: объединяет переменные окружения и сценария,
    /// создаёт HTTP-клиент и настраивает BaseAddress по переменной <c>baseUrl</c>.
    /// </summary>
    /// <param name="scenario">Сценарий.</param>
    /// <param name="ct">Токен отмены запуска.</param>
    /// <returns>Готовый контекст выполнения.</returns>
    public Task<ExecutionContext> CreateAsync(TestScenario scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ct.ThrowIfCancellationRequested();

        var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (scenario.Environment?.Variables is not null)
        {
            foreach (var pair in scenario.Environment.Variables)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        if (scenario.Variables is not null)
        {
            foreach (var pair in scenario.Variables)
            {
                variables[pair.Key] = pair.Value;
            }
        }

        var httpClient = _httpClientFactory.CreateClient();
        if (variables.TryGetValue("baseUrl", out var baseUrlValue))
        {
            var baseUrl = baseUrlValue?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(baseUrl) &&
                Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps))
            {
                httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            }
        }

        var context = new ExecutionContext
        {
            HttpClient = httpClient,
            Logger = _loggerFactory.CreateLogger("TestBuilder.Execution"),
            CancellationToken = ct,
            Templates = _templates,
            Variables = variables,
        };

        return Task.FromResult(context);
    }
}
