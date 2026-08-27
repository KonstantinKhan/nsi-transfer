using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.DAL.Interfaces.MessageBrokers;

namespace NsiTransfer.DAL.Services;

public class ConnectionFactoryCreator(IServiceProvider serviceProvider) : IConnectionFactory
{
    public RabbitMQ.Client.IConnection CreateConnection()
    {
        using var scope = serviceProvider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<RabbitMqCreds>>();
        var creds = options.CurrentValue;

        var factory = new RabbitMQ.Client.ConnectionFactory
        {
            HostName = creds.Host,
            Port = creds.Port,
            UserName = creds.Username,
            Password = creds.Password,
            VirtualHost = creds.VirtualHost,
            
            RequestedHeartbeat = TimeSpan.FromSeconds(60), // Для реагирования на отсутствие heartbeat необходимо сделать обработчик ConnectionShutdown
            ContinuationTimeout = TimeSpan.FromSeconds(10), // Таймаут для RPC-операций с сервером (типа QueueDeclare, ExchangeDeclare). Если ответ от брокера не пришел за 10 секунд - выбрасывается TimeoutException.
            HandshakeContinuationTimeout = TimeSpan.FromSeconds(10), // Таймаут для рукопожатия при установлении соединения. Если сервер не ответил на AMQP handshake за 10 секунд - соединение не устанавливается
        };
        
        return factory.CreateConnection();
    }
}