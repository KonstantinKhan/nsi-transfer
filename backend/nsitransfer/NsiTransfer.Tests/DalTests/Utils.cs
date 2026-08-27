using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

namespace NsiTransfer.Tests.DalTests;

public static class Utils
{
    public static MessageParams GetDefaultMessageParams()
    {
        return new MessageParams
        {
            ExchangeName = "test_exchange",
            QueueName = "test_queue",
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString(),
            MessageBody = "{\"key\": \"value\"}"
        };
    }
}