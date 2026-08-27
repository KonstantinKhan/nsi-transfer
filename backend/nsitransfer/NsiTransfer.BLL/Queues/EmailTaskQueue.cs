using NsiTransfer.BLL.Interfaces.Services;
using System.Threading.Channels;

namespace NsiTransfer.BLL.Queues;

internal class EmailTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _queue;

    private const string _queueName = "email";
    public string QueueName => _queueName;

    public EmailTaskQueue()
    {
        // По умолчанию тут ставится настройка BoundedChannelFullMode.Wait
        // Вызов WriteAsync ожидает доступность пространства для завершения операции записи.
        // Поэтому стоит всё-таки вызывать DequeueAsync из фонового потока
        var options = new BoundedChannelOptions(100);
        _queue = Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(options);
    }

    public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        return workItem;
    }

    public async Task QueueBackgroundWorkItem(Func<IServiceProvider, CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem);
    }
}
