using RabbitMQ.Client;

namespace NsiTransfer.DAL.Interfaces.MessageBrokers;

public interface IConnectionFactory
{
    IConnection CreateConnection();
}