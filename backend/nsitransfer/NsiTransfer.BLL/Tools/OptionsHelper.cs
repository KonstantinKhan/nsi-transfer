using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NsiTransfer.BLL.Interfaces.Services;
using System.Threading;

namespace NsiTransfer.BLL.Tools;

public sealed class OptionsHelper<T> : IOptionsHelper<T>, IDisposable where T : class
{
    private readonly IOptionsMonitor<T> _optionsMonitor;
    private readonly ILogger<OptionsHelper<T>> _logger;

    // Объект блокировки _cacheLock удален. 
    // Для ссылочных типов (class) операция присваивания в .NET является атомарной.
    private T? _lastValidValue;

    public OptionsHelper(IOptionsMonitor<T> optionsMonitor, ILogger<OptionsHelper<T>> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        if (TryGetCurrentValue(out var initial))
        {
            _lastValidValue = initial;
        }
    }

    public bool TryGetCurrentValue(out T? value)
    {
        try
        {
            value = _optionsMonitor.CurrentValue;
            Interlocked.Exchange(ref _lastValidValue, value);

            return true;
        }
        catch (OptionsValidationException ex)
        {
            _logger.LogWarning("Не удалось получить текущее значение конфигурации {OptionsType}: {Errors}", typeof(T).Name, string.Join("; ", ex.Failures));
            value = null;
            return false;
        }
    }

    public T? GetCurrentValueOrLastValid()
    {
        if (TryGetCurrentValue(out var value))
        {
            return value;
        }

        var lastValid = Volatile.Read(ref _lastValidValue);

        if (lastValid is not null)
        {
            _logger.LogWarning("Конфигурация {OptionsType} временно невалидна, используется последнее валидное значение.", typeof(T).Name);
        }

        return lastValid;
    }

    public IDisposable OnChange(Action<T?, bool> listener)
    {
        return _optionsMonitor.OnChange(_ =>
        {
            var isValid = TryGetCurrentValue(out var value);
            listener(value, isValid);
        });
    }

    public void Dispose()
    {
        // Подписки снимаются вызывающим кодом через IDisposable, возвращённый из OnChange.
    }
}