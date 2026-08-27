using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Contract.ConfigModels;

public class PolynomAuthCreds
{
    [Required(ErrorMessage = "Параметр '" + nameof(Username) + "' (Логин) обязателен.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр '" + nameof(Password) + "' (Пароль) обязателен.")]
    public string Password { get; set; } = string.Empty;
}
