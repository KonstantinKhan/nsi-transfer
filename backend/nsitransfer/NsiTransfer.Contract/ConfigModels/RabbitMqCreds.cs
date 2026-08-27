using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Contract.ConfigModels;

public class RabbitMqCreds
{
    [Required(ErrorMessage = "Параметр '" + nameof(Host) + "' (Хост RabbitMQ) обязателен.")]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Параметр '" + nameof(Port) + "' должен быть числом от 1 до 65535.")]
    public int Port { get; set; }

    [Required(ErrorMessage = "Параметр '" + nameof(Username) + "' (Логин) обязателен.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр '" + nameof(Password) + "' (Пароль) обязателен.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр '" + nameof(VirtualHost) + "' (Виртуальный хост RabbitMQ) обязателен.")]
    public string VirtualHost { get; set; } = string.Empty;
}
