using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Contract.ConfigModels;

public class PolynomConfig
{
    [Required(ErrorMessage = "Параметр '" + nameof(Address) + "' (Адрес сервера Polynom) обязателен.")]
    public string Address { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр '" + nameof(DbName) + "' (Имя базы данных) обязателен.")]
    public string DbName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр '" + nameof(TimeZoneId) + "' (Идентификатор часового пояса) обязателен.")]
    public string TimeZoneId { get; set; } = string.Empty;
}
