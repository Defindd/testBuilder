using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Execution;
using TestBuilder.Core.Scenarios;
using TestBuilder.Core.Templating;

namespace TestBuilder.Core.DependencyInjection;

/// <summary>
/// Регистрация сервисов ядра Test Builder.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует реестр блоков, движок выполнения, сервис сценариев, движок шаблонов
    /// и фабрику HTTP-клиентов с клиентом по умолчанию.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configure">Необязательная настройка параметров ядра.</param>
    /// <returns>Та же коллекция сервисов для цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> равен <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Параметры содержат некорректный базовый URL.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Таймаут по умолчанию не положительный.</exception>
    public static IServiceCollection AddTestBuilderCore(
        this IServiceCollection services,
        Action<TestBuilderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new TestBuilderOptions();
        configure?.Invoke(options);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<BlockRegistry>();
        services.AddSingleton<IBlockRegistry>(sp => sp.GetRequiredService<BlockRegistry>());
        services.AddSingleton<ExecutionContextFactory>();

        services.AddHttpClient(string.Empty, client =>
        {
            client.Timeout = options.DefaultHttpTimeout;

            if (!string.IsNullOrWhiteSpace(options.DefaultBaseUrl))
            {
                // Завершающий слэш нужен, чтобы относительные URL не отбрасывали последний сегмент base path.
                client.BaseAddress = new Uri(options.DefaultBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            }
        });

        services.AddSingleton(sp => new TemplateEngine(sp.GetService<ILogger<TemplateEngine>>()));

        services.AddTransient(sp => new TestRunner(
            sp.GetService<ILogger<TestRunner>>() ?? NullLogger<TestRunner>.Instance));

        services.AddSingleton(sp => new ScenarioService(
            sp.GetRequiredService<IBlockRegistry>(),
            sp.GetService<ILogger<ScenarioService>>()));

        return services;
    }
}
