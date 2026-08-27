namespace NsiTransfer.Contract.Exceptions;

public class RabbitMqPublishingAttemptFailedException : Exception
{
    public RabbitMqPublishingAttemptFailedException(string message) : base(message) { }
    
    public RabbitMqPublishingAttemptFailedException(string message, Exception innerException) : base(message, innerException) { }
}