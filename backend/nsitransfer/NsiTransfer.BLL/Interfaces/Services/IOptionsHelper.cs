namespace NsiTransfer.BLL.Interfaces.Services;

public interface IOptionsHelper<T> where T : class
{
    /// <summary>
    /// Пытается получить текущее значение опций без исключений.
    /// При ошибке валидации возвращает false, value будет null.
    /// </summary>
    bool TryGetCurrentValue(out T? value);

    /// <summary>
    /// Возвращает текущее валидное значение опций, либо, если текущее значение
    /// невалидно, последнее успешно полученное значение (если оно было).
    /// Возвращает null, если валидного значения не было получено ни разу.
    /// </summary>
    T? GetCurrentValueOrLastValid();

    /// <summary>
    /// Подписка на изменения файла конфигурации, безопасная к исключениям валидации.
    /// В колбэк всегда передаётся (значение, признак валидности) — даже если новое
    /// значение опций невалидно, обработчик не упадёт с необработанным исключением.
    /// </summary>
    IDisposable OnChange(Action<T?, bool> listener);
}
