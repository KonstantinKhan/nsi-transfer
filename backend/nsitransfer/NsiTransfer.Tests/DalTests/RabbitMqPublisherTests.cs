using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Network.MessageBrokers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace NsiTransfer.Tests.DalTests
{
    [TestFixture]
    public class RabbitMqPublisherTests
    {
        private Mock<DAL.Interfaces.MessageBrokers.IConnectionFactory> _mockConnectionFactory;
        private Mock<IConnection> _mockConnection;
        private Mock<IModel> _mockChannel;
        private Mock<IBasicProperties> _mockBasicProperties;
        private Mock<ILogger<RabbitMqPublisher>> _mockLogger;
        private Mock<IOptions<RabbitMqRetryParams>> _mockRetryParams;
        private RabbitMqPublisher _publisher;

        [SetUp]
        public void SetUp()
        {
            _mockConnectionFactory = new Mock<DAL.Interfaces.MessageBrokers.IConnectionFactory>();
            _mockConnection = new Mock<IConnection>();
            _mockChannel = new Mock<IModel>();
            _mockLogger = new Mock<ILogger<RabbitMqPublisher>>();
            _mockRetryParams = new Mock<IOptions<RabbitMqRetryParams>>();

            _mockLogger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
            _mockLogger.Setup(x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()));


            _mockConnectionFactory.Setup(f => f.CreateConnection()).Returns(_mockConnection.Object);
            _mockConnection.Setup(c => c.CreateModel()).Returns(_mockChannel.Object);
            _mockConnection.Setup(c => c.IsOpen).Returns(true);
            _mockConnection.Setup(c => c.Protocol).Returns(() => Protocols.AMQP_0_9_1);
            _mockChannel.Setup(c => c.IsOpen).Returns(true);
            _mockChannel.Setup(c => c.NextPublishSeqNo).Returns(1);
            _mockBasicProperties = new Mock<IBasicProperties>();
            _mockChannel.Setup(c => c.CreateBasicProperties()).Returns(_mockBasicProperties.Object);

            _mockRetryParams.Setup(p => p.Value).Returns(new RabbitMqRetryParams
            {
                MaxRetryAttempts = 3,
                RetryIntervalInSeconds = 1,
                ConfirmationTimeOutInSeconds = 5
            });

            _publisher = new RabbitMqPublisher(_mockConnectionFactory.Object, _mockLogger.Object, _mockRetryParams.Object);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_publisher != null)
            {
                await _publisher.DisposeAsync();
            }
        }

        #region Validation Tests

        [Test]
        public async Task PublishAsync_WithNullQueueName_ThrowsArgumentException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(async () => await _publisher.PublishAsync("test_exchange", null, "text/plain", "msg_1", "hello"));
            Assert.ThrowsAsync<ArgumentException>(async () => await _publisher.PublishAsync("test_exchange", "test_queue", "text/plain", "", "hello"));
            Assert.ThrowsAsync<ArgumentException>(async () => await _publisher.PublishAsync("test_exchange", "test_queue", "text/plain", "msg_1", ""));
        }

        #endregion

        #region Happy Path & Basic Scenarios

        [Test]
        public async Task PublishAsync_ValidData_ReturnsAck()
        {
            var defaultParams = Utils.GetDefaultMessageParams();
            ulong deliveryTag = 1;
            _mockChannel.Setup(c => c.NextPublishSeqNo).Returns(deliveryTag);

            SetupBasicPublishAndCallbackAfter(() =>
            {
                // Эмулируем ответ от RabbitMQ строго после вызова BasicPublish
                _mockChannel.Raise(c => c.BasicAcks += null, new BasicAckEventArgs 
                { 
                    DeliveryTag = deliveryTag, 
                    Multiple = false
                });
            });

            // Act: Запускаем публикацию в фоне
            var result = await _publisher.PublishAsync(defaultParams.ExchangeName, defaultParams.QueueName, defaultParams.ContentType, defaultParams.MessageId, defaultParams.MessageBody);

            // Assert: Ожидаем результат Ack
            Assert.That(result, Is.EqualTo(RabbitMqPublishingResultEnum.Ack));

            var bodyBytes = Encoding.UTF8.GetBytes(defaultParams.MessageBody);

            // Проверяем, что BasicPublish был вызван с правильными параметрами
            _mockChannel.Verify(c => c.BasicPublish(
                defaultParams.ExchangeName,
                defaultParams.QueueName,
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                //It.Is<ReadOnlyMemory<byte>>(mem => mem.ToArray().SequenceEqual(bodyBytes))
                It.Is<ReadOnlyMemory<byte>>(mes => CompareMessages(mes, defaultParams.MessageBody))
            ), Times.Once);
        }

        [Test]
        public async Task PublishAsync_WhenConnectionIsBlocked_ReturnsConnectionBlocked()
        {
            ulong deliveryTag = 1;
            _mockChannel.Setup(c => c.NextPublishSeqNo).Returns(deliveryTag);
            SetupBasicPublishAndCallbackAfter(() =>
            {
                // Эмулируем ответ от RabbitMQ строго после вызова BasicPublish
                _mockChannel.Raise(c => c.BasicAcks += null, new BasicAckEventArgs
                {
                    DeliveryTag = deliveryTag,
                    Multiple = false
                });
            });

            var defaultParams = Utils.GetDefaultMessageParams();
            var result1 = await _publisher.PublishAsync(defaultParams.ExchangeName, defaultParams.QueueName, defaultParams.ContentType, defaultParams.MessageId, defaultParams.MessageBody);
            Assert.That(result1, Is.EqualTo(RabbitMqPublishingResultEnum.Ack));


            SetupBasicPublishAndCallbackAfter(() => { });
            _mockConnection.Raise(c => c.ConnectionBlocked += null, new ConnectionBlockedEventArgs("memory alarm"));
            var result2 = await _publisher.PublishAsync(defaultParams.ExchangeName, defaultParams.QueueName, defaultParams.ContentType, defaultParams.MessageId, defaultParams.MessageBody);
            Assert.That(result2, Is.EqualTo(RabbitMqPublishingResultEnum.ConnectionBlocked));


            _mockChannel.Verify(c => c.BasicPublish(
                defaultParams.ExchangeName,
                defaultParams.QueueName,
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.Is<ReadOnlyMemory<byte>>(mes => CompareMessages(mes, defaultParams.MessageBody))
            ), Times.Once);
        }

        [Test]
        public async Task PublishAsync_WhenNackReceived_ReturnsNack()
        {
            var defaultParams = Utils.GetDefaultMessageParams();
            ulong deliveryTag = 1;
            _mockChannel.Setup(c => c.NextPublishSeqNo).Returns(deliveryTag);

            SetupBasicPublishAndCallbackAfter(() =>
            {
                _mockChannel.Raise(c => c.BasicNacks += null, new BasicNackEventArgs
                {
                    DeliveryTag = deliveryTag,
                    Multiple = false,
                    Requeue = false
                });
            });

            var result = await _publisher.PublishAsync(defaultParams.ExchangeName, defaultParams.QueueName, defaultParams.ContentType, defaultParams.MessageId, defaultParams.MessageBody);

            Assert.That(result, Is.EqualTo(RabbitMqPublishingResultEnum.Nack));

            _mockChannel.Verify(c => c.BasicPublish(
                defaultParams.ExchangeName,
                defaultParams.QueueName,
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.Is<ReadOnlyMemory<byte>>(mes => CompareMessages(mes, defaultParams.MessageBody))
            ), Times.Once);
        }

        #endregion

        #region Retry & Error Handling

        [Test]
        public async Task PublishAsync_WhenConfirmationTimesOut_ReturnsTimedOut()
        {
            var defaultParams = Utils.GetDefaultMessageParams();
            ulong deliveryTag = 1;
            _mockChannel.Setup(c => c.NextPublishSeqNo).Returns(deliveryTag);

            // Важно: канал должен оставаться открытым, иначе вернется ChannelClosed, а не TimedOut
            _mockChannel.Setup(c => c.IsOpen).Returns(true);

            var result = await _publisher.PublishAsync(defaultParams.ExchangeName, defaultParams.QueueName, defaultParams.ContentType, defaultParams.MessageId, defaultParams.MessageBody);

            Assert.That(result, Is.EqualTo(RabbitMqPublishingResultEnum.TimedOut));

            // Дополнительно проверяем, что сообщение было отправлено (BasicPublish вызван)
            _mockChannel.Verify(c => c.BasicPublish(
                defaultParams.ExchangeName,
                defaultParams.QueueName,
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.Is<ReadOnlyMemory<byte>>(mes => CompareMessages(mes, defaultParams.MessageBody))
            ), Times.Once);
        }

        [Test]
        public async Task PublishAsync_ChannelClosesDuringPublish_RetriesAndSucceeds()
        {
            ulong deliveryTag = 1;
            _mockChannel.Setup(c => c.NextPublishSeqNo).Returns(deliveryTag);

            int callCount = 0;

            // Настраиваем BasicPublish так, чтобы первый вызов выбрасывал исключение,
            // а второй вызов успешно завершался и сразу эмулировал получение Ack
            SetupBasicPublishAndCallbackAfter(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Первая попытка: эмулируем обрыв канала
                    throw new AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Library, 0, "Channel closed"));
                }
                else if (callCount == 2)
                {
                    // Вторая попытка: сообщение успешно ушло.
                    // СРАЗУ же эмулируем ответ от брокера (Ack), пока мы находимся внутри вызова BasicPublish.
                    // В этот момент новый TaskCompletionSource уже добавлен в _pendingSendings и ждет подтверждения.
                    _mockChannel.Raise(c => c.BasicAcks += null, new BasicAckEventArgs
                    {
                        DeliveryTag = deliveryTag,
                        Multiple = false
                    });
                }
            });

            var defaultParams = Utils.GetDefaultMessageParams();

            var result = await _publisher.PublishAsync(defaultParams.ExchangeName, defaultParams.QueueName, defaultParams.ContentType, defaultParams.MessageId, defaultParams.MessageBody);

            Assert.That(result, Is.EqualTo(RabbitMqPublishingResultEnum.Ack));

            // Проверяем, что метод BasicPublish был вызван ровно 2 раза
            _mockChannel.Verify(c => c.BasicPublish(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<IBasicProperties>(),
                It.Is<ReadOnlyMemory<byte>>(mes => CompareMessages(mes, defaultParams.MessageBody))
            ),Times.Exactly(2));
        }

        [Test]
        public async Task PublishAsync_ConnectionFailsMaxRetries_ThrowsAggregateException()
        {
            // Arrange: Factory всегда возвращает соединение, которое падает при CreateModel
            _mockConnection.Setup(c => c.CreateModel()).Throws(new BrokerUnreachableException(new Exception("Network error")));

            var ex = Assert.ThrowsAsync<AggregateException>(async () =>
                await _publisher.PublishAsync("test_exchange", "test_queue", "text/plain", "msg_1", "hello"));
        }

        #endregion

        #region Lifecycle Tests

        [Test]
        public async Task PublishAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            await _publisher.DisposeAsync();

            Assert.ThrowsAsync<ObjectDisposedException>(async () => await _publisher.PublishAsync("test_exchange", "test_queue", "text/plain", "msg_1", "hello"));
        }

        [Test]
        public async Task DisposeAsync_ClosesAndDisposesResources()
        {
            await _publisher.PublishAsync("test_exchange", "test_queue", "text/plain", "msg_1", "hello"); // Быстрый фейл или успех не важен, главное инициализация

            await _publisher.DisposeAsync();

            _mockChannel.Verify(c => c.Close(), Times.Once);
            _mockChannel.Verify(c => c.Dispose(), Times.Once);
            _mockConnection.Verify(c => c.Close(), Times.Once);
            _mockConnection.Verify(c => c.Dispose(), Times.Once);
        }

        #endregion
        
        
        private void SetupBasicPublishAndCallbackAfter(Action callback)
        {
            _mockChannel.Setup(c => c.BasicPublish(
                            It.IsAny<string>(),                 // exchange
                            It.IsAny<string>(),                 // routingKey
                            It.IsAny<bool>(),                   // mandatory
                            It.IsAny<IBasicProperties>(),       // properties
                            It.IsAny<ReadOnlyMemory<byte>>())   // message (byte[])
                            )
            .Callback(callback);
        }

        private bool CompareMessages(ReadOnlyMemory<byte> message, string actualMessage)
        {
            var bytes = Encoding.UTF8.GetBytes(actualMessage);
            return message.Span.SequenceEqual(bytes);
        }
    }
}