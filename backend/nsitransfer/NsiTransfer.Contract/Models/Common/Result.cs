namespace NsiTransfer.Contract.Models.Common;

/// <summary>
/// Обёртка для результата операции с поддержкой успеха/ошибки
/// </summary>
/// <typeparam name="T">Тип данных результата</typeparam>
public class Result<T>
{
    public T? Data { get; private set; }
    public bool IsSuccess { get; private set; }

    private List<string>? _errors = null;

    public string ErrorMessage =>
        _errors == null
        ? string.Empty
        : string.Join("\n", _errors.Select((e, i) => $"{new string(' ', i * 2)}- {e}"));

    public void AddError(string error)
    {
        _errors ??= [];
        if (!string.IsNullOrWhiteSpace(error)) _errors.Add(error);

        IsSuccess = false;
    }

    private Result() { }

    /// <summary>
    /// Создаёт успешный результат с данными
    /// </summary>
    /// <param name="data">Данные результата</param>
    public static Result<T> Success(T data) => new()
    {
        Data = data,
        IsSuccess = true,
        _errors = null
    };

    /// <summary>
    /// Создаёт неуспешный результат с сообщением об ошибке
    /// </summary>
    /// <param name="errorMessage">Сообщение об ошибке</param>
    public static Result<T> Failure(string errorMessage) => new()
    {
        Data = default,
        IsSuccess = false,
        _errors = [errorMessage]
    };

    // --- ОПЕРАТОРЫ БЕЗ StatusCode ---

    /// <summary>
    /// Неявное преобразование данных в успешный Result
    /// </summary>
    public static implicit operator Result<T>(T data) => Success(data);

    /// <summary>
    /// Неявное преобразование строки в неуспешный Result
    /// </summary>
    public static implicit operator Result<T>(string errorMessage) => Failure(errorMessage);
}

/// <summary>
/// Обёртка для результата операции без данных (только успех/ошибка)
/// </summary>
public class Result
{
    public bool IsSuccess { get; private set; }
    private List<string>? _errors = null;

    public string ErrorMessage =>
        _errors == null 
        ? string.Empty
        : string.Join("\n", _errors.Select((e, i) => $"{new string(' ', i * 2)}- {e}"));


    private Result() { }

    /// <summary>
    /// Создаёт успешный результат
    /// </summary>
    public static Result Success() => new()
    {
        IsSuccess = true,
        _errors = null
    };

    /// <summary>
    /// Создаёт неуспешный результат с сообщением об ошибке
    /// </summary>
    public static Result Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        _errors = [errorMessage]
    };

    public static implicit operator Result(string errorMessage) => Failure(errorMessage);

    public void AddError(string error)
    {
        _errors ??= [];
        if (!string.IsNullOrWhiteSpace(error)) _errors.Add(error);

        IsSuccess = false;
    }
}