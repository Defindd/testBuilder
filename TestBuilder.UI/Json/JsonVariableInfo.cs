namespace TestBuilder.UI.Json;

/// <summary>
/// JSON-переменная, доступная для вставки в поля последующих блоков.
/// </summary>
public sealed class JsonVariableInfo
{
    /// <summary>
    /// Создаёт описание переменной.
    /// </summary>
    /// <param name="name">Имя переменной.</param>
    /// <param name="paths">Доступные пути, включая корневой.</param>
    public JsonVariableInfo(string name, IReadOnlyList<string> paths)
    {
        Name = name;
        Paths = paths;
    }

    /// <summary>
    /// Имя переменной.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Пути вида <c>request.customer.id</c>.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }
}
