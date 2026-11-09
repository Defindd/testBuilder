namespace TestBuilder.Elma365;

/// <summary>
/// Базовое исключение клиента Elma365.
/// </summary>
public class Elma365Exception : Exception
{
    /// <summary>
    /// HTTP-статус ответа, если он был получен.
    /// </summary>
    public int? StatusCode { get; }

    /// <summary>
    /// Тело ответа Elma365, если оно было получено.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Создаёт исключение без HTTP-контекста.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    public Elma365Exception(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Создаёт исключение без HTTP-контекста, сохраняя внутреннее исключение.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public Elma365Exception(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Создаёт исключение с HTTP-контекстом.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="statusCode">HTTP-статус.</param>
    /// <param name="responseBody">Тело ответа.</param>
    public Elma365Exception(string message, int statusCode, string? responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

/// <summary>
/// Ошибка авторизации в Elma365 (HTTP 401/403).
/// </summary>
public sealed class Elma365AuthException : Elma365Exception
{
    /// <summary>
    /// Создаёт исключение авторизации.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="statusCode">HTTP-статус.</param>
    /// <param name="responseBody">Тело ответа.</param>
    public Elma365AuthException(string message, int statusCode, string? responseBody)
        : base(message, statusCode, responseBody)
    {
    }
}

/// <summary>
/// Объект Elma365 не найден (HTTP 404).
/// </summary>
public sealed class Elma365NotFoundException : Elma365Exception
{
    /// <summary>
    /// Создаёт исключение «не найдено».
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="statusCode">HTTP-статус.</param>
    /// <param name="responseBody">Тело ответа.</param>
    public Elma365NotFoundException(string message, int statusCode, string? responseBody)
        : base(message, statusCode, responseBody)
    {
    }
}

/// <summary>
/// Ошибка на стороне сервера Elma365 или исчерпаны повторы.
/// </summary>
public sealed class Elma365ServerException : Elma365Exception
{
    /// <summary>
    /// Создаёт серверное исключение.
    /// </summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="statusCode">HTTP-статус.</param>
    /// <param name="responseBody">Тело ответа.</param>
    public Elma365ServerException(string message, int statusCode, string? responseBody)
        : base(message, statusCode, responseBody)
    {
    }
}
