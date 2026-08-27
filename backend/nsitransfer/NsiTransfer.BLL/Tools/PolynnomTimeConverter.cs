using Microsoft.Extensions.Options;
using NsiTransfer.Contract.ConfigModels;

namespace NsiTransfer.BLL.Tools;

public class PolynomTimeConverter
{
    private readonly IOptionsMonitor<PolynomConfig> _optionsMonitor;

    public PolynomTimeConverter(IOptionsMonitor<PolynomConfig> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    /// <summary>
    /// Конвертирует наше внутреннее UTC время в локальное время для отправки в Полином.
    /// </summary>
    public DateTime ConvertToPolynomTime(DateTime utcDateTime)
    {
        if (utcDateTime == DateTime.MinValue || utcDateTime == DateTime.MaxValue) return utcDateTime;
        if (utcDateTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Ожидается время в формате UTC", nameof(utcDateTime));

        // Убираем метку времени, чтобы Полином получил "Unspecified"
        var timeZoneId = _optionsMonitor.CurrentValue.TimeZoneId;
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        return DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Конвертирует время, полученное из Полинома, в наше внутреннее UTC.
    /// </summary>
    public DateTime ConvertFromPolynomTime(DateTime polynomDateTime)
    {
        var localTime = DateTime.SpecifyKind(polynomDateTime, DateTimeKind.Unspecified);
        var timeZoneId = _optionsMonitor.CurrentValue.TimeZoneId;
        return TimeZoneInfo.ConvertTimeToUtc(localTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
    }
}
