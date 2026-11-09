using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TestBuilder.Core.Blocks;
using TestBuilder.Core.Serialization;

namespace TestBuilder.Core.Scenarios;

/// <summary>
/// Загрузка, сохранение и базовая валидация сценариев <c>.tbtest</c>.
/// </summary>
public sealed class ScenarioService
{
    private const string FileExtension = ".tbtest";

    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<ScenarioService> _logger;

    /// <summary>
    /// Создаёт сервис сценариев.
    /// </summary>
    /// <param name="blockRegistry">Реестр блоков для полиморфной десериализации.</param>
    /// <param name="logger">Логгер; при отсутствии используется <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="blockRegistry"/> равен <see langword="null"/>.</exception>
    public ScenarioService(IBlockRegistry blockRegistry, ILogger<ScenarioService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(blockRegistry);

        _serializerOptions = JsonSetup.CreateDefault(blockRegistry);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ScenarioService>.Instance;
    }

    /// <summary>
    /// Загружает сценарий из файла.
    /// </summary>
    /// <param name="path">Путь к файлу <c>.tbtest</c>.</param>
    /// <returns>Загруженный и проверенный сценарий.</returns>
    /// <exception cref="ArgumentException">Путь пуст.</exception>
    /// <exception cref="FileNotFoundException">Файл не существует.</exception>
    /// <exception cref="InvalidDataException">Файл содержит некорректный JSON или не является сценарием.</exception>
    /// <exception cref="ValidationException">Сценарий не прошёл валидацию.</exception>
    public TestScenario Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Файл сценария не найден: '{fullPath}'.", fullPath);
        }

        TestScenario? scenario;
        try
        {
            var json = File.ReadAllText(fullPath, Encoding.UTF8);
            scenario = JsonSerializer.Deserialize<TestScenario>(json, _serializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Не удалось прочитать сценарий '{fullPath}': некорректный JSON.",
                ex);
        }

        if (scenario is null)
        {
            throw new InvalidDataException($"Файл '{fullPath}' не содержит объекта сценария.");
        }

        Normalize(scenario);
        Validate(scenario);

        _logger.LogInformation("Сценарий '{ScenarioName}' загружен из '{Path}'.", scenario.Name, fullPath);
        return scenario;
    }

    /// <summary>
    /// Сохраняет сценарий в файл. Запись выполняется атомарно через временный файл.
    /// </summary>
    /// <param name="scenario">Сценарий.</param>
    /// <param name="path">Путь назначения.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scenario"/> равен <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Путь пуст.</exception>
    public void Save(TestScenario scenario, string path)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Сценарий сохраняется с нестандартным расширением '{Extension}'.",
                Path.GetExtension(fullPath));
        }

        Normalize(scenario);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(scenario, _serializerOptions);
        var tempPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            File.WriteAllText(tempPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        _logger.LogInformation("Сценарий '{ScenarioName}' сохранён в '{Path}'.", scenario.Name, fullPath);
    }

    /// <summary>
    /// Проверяет сценарий и нормализует необязательные поля.
    /// Пустые названия шагов заменяются на «Шаг N», пустым идентификаторам присваиваются новые значения.
    /// </summary>
    /// <param name="scenario">Проверяемый сценарий.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scenario"/> равен <see langword="null"/>.</exception>
    /// <exception cref="ValidationException">Обнаружены ошибки валидации.</exception>
    public void Validate(TestScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        Normalize(scenario);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(scenario.Name))
        {
            scenario.Name = "Новый сценарий";
        }

        if (string.IsNullOrWhiteSpace(scenario.Environment.Name))
        {
            scenario.Environment.Name = "local";
        }

        var ids = new HashSet<Guid>();
        for (var index = 0; index < scenario.Blocks.Count; index++)
        {
            var block = scenario.Blocks[index];
            var displayNumber = index + 1;

            if (block is null)
            {
                errors.Add($"Шаг #{displayNumber}: блок равен null.");
                continue;
            }

            if (block.Id == Guid.Empty)
            {
                block.Id = Guid.NewGuid();
            }

            if (!ids.Add(block.Id))
            {
                errors.Add($"Шаг #{displayNumber}: идентификатор блока '{block.Id}' повторяется.");
            }

            if (string.IsNullOrWhiteSpace(block.Title))
            {
                block.Title = $"Шаг {displayNumber}";
            }

            if (string.IsNullOrWhiteSpace(block.BlockType))
            {
                errors.Add($"Шаг #{displayNumber} ('{block.Title}'): не задан тип блока.");
                continue;
            }

            if (block is IValidatableBlock validatable)
            {
                try
                {
                    foreach (var error in validatable.Validate())
                    {
                        if (!string.IsNullOrWhiteSpace(error))
                        {
                            errors.Add($"Шаг #{displayNumber} ('{block.Title}'): {error}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Шаг #{displayNumber} ('{block.Title}'): ошибка валидации блока: {ex.Message}");
                }
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("Валидация сценария '{ScenarioName}' выявила {Count} ошибок.", scenario.Name, errors.Count);
            throw new ValidationException(errors);
        }
    }

    private static void Normalize(TestScenario scenario)
    {
        scenario.Name ??= string.Empty;
        scenario.Environment ??= new Environment();
        scenario.Environment.Name ??= string.Empty;
        scenario.Environment.Variables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        scenario.Variables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        scenario.Blocks ??= new List<ITestBlock>();
    }
}
