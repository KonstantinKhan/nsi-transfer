using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Contract.ConfigModels;

public class SmtpSettings
{
    [Required(ErrorMessage = "Параметр 'Server' (SMTP хост) обязателен для заполнения.")]
    public string Server { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр 'Port' обязателен для заполнения.")]
    [Range(1, 65535, ErrorMessage = "Порт должен быть числом от 1 до 65535.")]
    public int Port { get; set; }

    [Required(ErrorMessage = "Параметр 'SenderName' (Имя отправителя) обязателен.")]
    public string SenderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр 'SenderEmail' (Email отправителя) обязателен.")]
    [EmailAddress(ErrorMessage = "Параметр 'SenderEmail' имеет некорректный формат email-адреса.")]
    public string SenderEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр 'Username' (Логин) обязателен.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Параметр 'Password' (Пароль) обязателен.")]
    public string Password { get; set; } = string.Empty;

    // Опционально: использовать ли SSL/TLS. По умолчанию true для современных серверов.
    public bool UseSsl { get; set; } = true;
}