namespace NsiTransfer.Presentation.BackgroundServices;


public sealed class LogFolderCleanupService : BackgroundService
{
    private readonly string _logsPath;
    private readonly long _maxFolderSizeBytes;
    private readonly TimeSpan _checkInterval;
    private readonly ILogger<LogFolderCleanupService> _logger;

    public LogFolderCleanupService(
        string logsPath,
        long maxFolderSizeBytes,
        TimeSpan checkInterval,
        ILogger<LogFolderCleanupService> logger)
    {
        _logsPath = logsPath;
        _maxFolderSizeBytes = maxFolderSizeBytes;
        _checkInterval = checkInterval;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                CleanupIfFolderExceededLimit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при автоочистке папки логов");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private void CleanupIfFolderExceededLimit()
    {
        if (!Directory.Exists(_logsPath))
        {
            return;
        }

        var directoryInfo = new DirectoryInfo(_logsPath);
        var logFiles = directoryInfo
            .GetFiles("log-*.txt", SearchOption.TopDirectoryOnly)
            .OrderBy(file => file.LastWriteTimeUtc)
            .ToList();

        if (logFiles.Count == 0)
        {
            return;
        }

        long totalSize = 0;
        foreach (var file in logFiles)
        {
            totalSize += file.Length;
        }

        if (totalSize <= _maxFolderSizeBytes)
        {
            return;
        }

        _logger.LogWarning(
            "Размер папки логов ({CurrentSize} байт) превысил лимит ({MaxSize} байт). Запуск очистки старых файлов.",
            totalSize,
            _maxFolderSizeBytes);

        foreach (var file in logFiles)
        {
            if (totalSize <= _maxFolderSizeBytes)
            {
                break;
            }

            try
            {
                file.Delete();
                totalSize -= file.Length;

                _logger.LogInformation(
                    "Удалён старый лог: {FileName}. Текущий размер папки: {CurrentSize} байт.",
                    file.Name,
                    totalSize);
            }
            catch (IOException ioEx)
            {
                _logger.LogWarning(ioEx, "Не удалось удалить файл лога {FileName}: файл используется другим процессом", file.Name);
            }
            catch (UnauthorizedAccessException accessEx)
            {
                _logger.LogWarning(accessEx, "Не удалось удалить файл лога {FileName}: нет прав доступа", file.Name);
            }
        }

        if (totalSize > _maxFolderSizeBytes)
        {
            _logger.LogWarning(
                "После очистки размер папки логов все ещё превышает лимит: {CurrentSize}/{MaxSize} байт.",
                totalSize,
                _maxFolderSizeBytes);
        }
    }
}
