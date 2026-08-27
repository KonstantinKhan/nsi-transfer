namespace NsiTransfer.BLL.Interfaces.Services;

public interface IBackgroundTaskQueue
{
    string QueueName { get; }

    /// <summary>
    /// Метод для помещения задачи в очередь
    /// </summary>
    /// <param name="workItem">Делегат, представляющий собой код, который должен будет запуститься фоновым сервисом</param>
    Task QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem);

    /// <summary>
    /// Метод для извлечения задачи из очереди (используется только Воркером)
    /// </summary>
    /// <param name="cancellationToken">Токен отмены для извлечения задачи из очереди</param>
    /// <returns>Задача, по завершению которой можно получить делегат, представляющий собой код, который должен будет запуститься фоновым сервисом</returns>
    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
