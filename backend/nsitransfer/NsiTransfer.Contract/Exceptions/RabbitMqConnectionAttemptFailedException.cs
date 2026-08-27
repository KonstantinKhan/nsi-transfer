namespace NsiTransfer.Contract.Exceptions;

public class RabbitMqConnectionAttemptFailedException : Exception
{
    public RabbitMqConnectionAttemptFailedException(string message) : base(message) { }
    
    public RabbitMqConnectionAttemptFailedException(string message, Exception innerException) : base(message, innerException) { }
}