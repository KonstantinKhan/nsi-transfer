using System.ComponentModel.DataAnnotations;

namespace NsiTransfer.Logging;

public class LoggingConfig
{
    public bool UseFileLogging { get; set; } = false;

    [Range(0, long.MaxValue, ErrorMessage = "Параметр '" + nameof(LogsMaxFolderSizeBytes) + "' (Максимальный размер папки логов) не может быть отрицательным.")]
    public long LogsMaxFolderSizeBytes { get; set; } = 0;

    [Range(1, int.MaxValue, ErrorMessage = "Параметр '" + nameof(LogsCleanupIntervalSeconds) + "' (Интервал очистки логов) должен быть больше 0.")]
    public int LogsCleanupIntervalSeconds { get; set; } = 60;
}
