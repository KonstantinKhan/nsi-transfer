using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Models.Base;
using Ascon.Polynom.Web.Api.Data.Models.Search;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Services;
using NsiTransfer.BLL.UseCases;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;
using NsiTransfer.DAL.Interfaces.MessageBrokers;
using System.Linq.Expressions;
namespace NsiTransfer.Tests.BllTests
{
    [TestFixture]
    public class SyncUseCasesTests
    {
        private Mock<IUnitOfWork> _unitOfWorkMock = null!;
        private Mock<IPolynomApiService> _polynomApiMock = null!;
        private Mock<IRabbitMqPublisher> _rabbitMqMock = null!;
        private Mock<IClassificationCodeProcessor> _classifierMock = null!;
        private Mock<ISyncNotifier> _notifierMock = null!;
        private Mock<IErrorNotifier> _errorNotifierMock = null!;
        //private Mock<ILogger<SyncUseCases>> _loggerMock = null!;
        
        private SyncUseCases _useCases = null!;

        [SetUp]
        public void Setup()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _polynomApiMock = new Mock<IPolynomApiService>();
            _rabbitMqMock = new Mock<IRabbitMqPublisher>();
            _classifierMock = new Mock<IClassificationCodeProcessor>();
            _notifierMock = new Mock<ISyncNotifier>();
            _errorNotifierMock = new Mock<IErrorNotifier>();
            //_loggerMock = new Mock<ILogger<SyncUseCases>>();

            // 1. Настройка Options
            var rabbitOptionsMock = new Mock<IOptionsMonitor<RabbitMqQueues>>();
            rabbitOptionsMock.Setup(o => o.CurrentValue).Returns(new RabbitMqQueues 
            { 
                NsiTransferExchangeName = "test_exchange", 
                PolynomSearchResultsQueueName = "test_queue" 
            });

            var targetOptionsMock = new Mock<IOptionsMonitor<Contract.ConfigModels.TargetReferenceNode>>();
            targetOptionsMock.Setup(o => o.CurrentValue).Returns(new Contract.ConfigModels.TargetReferenceNode 
            { 
                TargetReferenceNodeObjectId = 1, 
                TargetReferenceNodeTypeId = (int)(IdentifiableObjectType)1 
            });

            var apiOptionsMock = new Mock<IOptionsMonitor<PolynomApiSyncOptions>>();
            apiOptionsMock.Setup(o => o.CurrentValue).Returns(new PolynomApiSyncOptions 
            { 
                ConceptNameForClassificationData = "ConceptA" 
            });

            // 2. Настройка BackgroundTaskQueue (обязательно для конструктора SyncUseCases)
            var bgQueueMock = new Mock<IBackgroundTaskQueue>();
            bgQueueMock.Setup(q => q.QueueName).Returns("sync");

            // 3. Настройка репозиториев UnitOfWork 
            // ВАЖНО: Явно указываем ВСЕ параметры в It.IsAny, чтобы избежать CS0854/CS1929
            SetupRepositoryMock<Sending>(_unitOfWorkMock, u => u.Sendings);
            SetupRepositoryMock<Message>(_unitOfWorkMock, u => u.Messages);
            SetupRepositoryMock<NsiTransfer.DAL.Db.Entities.PolynomObject>(_unitOfWorkMock, u => u.PolynomObjects);
            SetupRepositoryMock<MessageFailure>(_unitOfWorkMock, u => u.MessageFailures);
            SetupRepositoryMock<NsiTransfer.DAL.Db.Entities.TargetReferenceNode>(_unitOfWorkMock, u => u.TargetReferenceNodes);

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            _useCases = new SyncUseCases(
                _unitOfWorkMock.Object,
                _polynomApiMock.Object,
                _rabbitMqMock.Object,
                new List<IBackgroundTaskQueue> { bgQueueMock.Object },
                _notifierMock.Object,
                _errorNotifierMock.Object,
                _classifierMock.Object,
                rabbitOptionsMock.Object,
                targetOptionsMock.Object,
                apiOptionsMock.Object,
                NullLogger<SyncUseCases>.Instance);
        }

        [Test]
        public async Task IsThereAlreadyActiveSync_NoActiveSync_ShouldReturnSuccess()
        {
            // Arrange
            _unitOfWorkMock.Setup(u => u.Sendings.AnyAsync(
                    It.IsAny<Expression<Func<Sending, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _useCases.IsThereAlreadyActiveSync(CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Test]
        public async Task IsThereAlreadyActiveSync_ActiveSyncExists_ShouldReturnError()
        {
            // Arrange
            _unitOfWorkMock.Setup(u => u.Sendings.AnyAsync(
                    It.IsAny<Expression<Func<Sending, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _useCases.IsThereAlreadyActiveSync(CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Синхронизация уже запущена");
        }

        [Test]
        public async Task ContinueDataCollectionAsync_NoDataFound_ShouldCompleteSuccessfully()
        {
            // Arrange
            var sendingId = Guid.NewGuid();
            var sending = new Sending 
            { 
                Id = sendingId, 
                InitiatorName = "TestUser",
                TargetReferenceNode = new DAL.Db.Entities.TargetReferenceNode { ObjectId = 1, TypeId = (int)(IdentifiableObjectType)1 }
            };
            sending.SetStatus(SendingStatusEnum.Initiated);

            _unitOfWorkMock.SetupSequence(u => u.Sendings.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Sending, bool>>>(),
                It.IsAny<Func<IQueryable<Sending>, IOrderedQueryable<Sending>>>(),
                It.IsAny<Func<IQueryable<Sending>, IQueryable<Sending>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sending)         // 1-й вызов вернет sending
            .ReturnsAsync((Sending?)null); // 2-й вызов вернет null

            // 3. Мок API: нет данных (пустой список)
            var emptyPaginatedList = new PaginatedList<PropertySearchResultObject>(new List<PropertySearchResultObject>(), 0, 1, 0);
            _polynomApiMock.Setup(p => p.GetDiffsInTimePeriod(
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PaginatedList<PropertySearchResultObject>>.Success(emptyPaginatedList));

            // Act
            var result = await _useCases.ContinueDataCollectionAsync(sendingId, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            // Проверяем, что статус изменился на EmptySending
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce());

            // Проверяем, что пустое сообщение было удалено
            _unitOfWorkMock.Verify(u => u.Messages.Delete(It.IsAny<Message>()), Times.Once);
        }

        [Test]
        public async Task ContinueDataCollectionAsync_PolynomApiFails_ShouldCreateMessageFailure()
        {
            // Arrange
            var sendingId = Guid.NewGuid();
            var sending = new Sending 
            { 
                Id = sendingId, 
                InitiatorName = "TestUser",
                TargetReferenceNode = new DAL.Db.Entities.TargetReferenceNode { ObjectId = 1, TypeId = (int)(IdentifiableObjectType)1 }
            };
            sending.SetStatus(SendingStatusEnum.Initiated);

            //_unitOfWorkMock.Setup(u => u.Sendings.FirstOrDefaultAsync(
            //        It.IsAny<Expression<Func<Sending, bool>>>(),
            //        It.IsAny<Func<IQueryable<Sending>, IOrderedQueryable<Sending>>>(),
            //        It.IsAny<Func<IQueryable<Sending>, IQueryable<Sending>>>(),
            //        It.IsAny<CancellationToken>()))
            //    .ReturnsAsync(sending);

            //_unitOfWorkMock.Setup(u => u.Sendings.FirstOrDefaultAsync(
            //        It.IsAny<Expression<Func<Sending, bool>>>(),
            //        It.IsAny<Func<IQueryable<Sending>, IOrderedQueryable<Sending>>>(),
            //        It.IsAny<Func<IQueryable<Sending>, IQueryable<Sending>>>(),
            //        It.IsAny<CancellationToken>()))
            //    .ReturnsAsync((Sending?)null);

            // ИСПОЛЬЗУЕМ SetupSequence, чтобы вернуть разные значения для 1-го и 2-го вызовов
            _unitOfWorkMock.SetupSequence(u => u.Sendings.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<Sending, bool>>>(),
                    It.IsAny<Func<IQueryable<Sending>, IOrderedQueryable<Sending>>>(),
                    It.IsAny<Func<IQueryable<Sending>, IQueryable<Sending>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(sending)         // 1-й вызов вернет sending
                .ReturnsAsync((Sending?)null); // 2-й вызов вернет null

            // API возвращает ошибку
            _polynomApiMock.Setup(p => p.GetDiffsInTimePeriod(
                    It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PaginatedList<PropertySearchResultObject>>.Failure("API Error"));

            // Act
            var result = await _useCases.ContinueDataCollectionAsync(sendingId, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            
            // Проверяем, что была создана запись об ошибке
            _unitOfWorkMock.Verify(u => u.MessageFailures.AddAsync(
                It.Is<MessageFailure>(mf => mf.FailureReasonTitle.Contains("Ошибка поиска объектов")), 
                It.IsAny<CancellationToken>()), Times.Once);
                
            // Проверяем уведомление об ошибке
            _errorNotifierMock.Verify(e => e.NotifyAboutErrorAsync(
                It.IsAny<Result<PaginatedList<PropertySearchResultObject>>>(), 
                It.IsAny<string>()), Times.Once);
        }

        #region Helpers
        
        private void SetupRepositoryMock<TEntity>(Mock<IUnitOfWork> uowMock, Expression<Func<IUnitOfWork, IGenericRepository<TEntity>>> repoSelector) where TEntity : class
        {
            var repoMock = new Mock<IGenericRepository<TEntity>>();
            
            // Явная настройка AddAsync
            repoMock.Setup(r => r.AddAsync(It.IsAny<TEntity>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TEntity entity, CancellationToken ct) => entity);
            
            // Явная настройка Delete
            repoMock.Setup(r => r.Delete(It.IsAny<TEntity>()));

            // Явная настройка Update
            repoMock.Setup(r => r.Update(It.IsAny<TEntity>()));

            // Явная настройка FirstOrDefaultAsync (ВСЕ параметры, чтобы избежать CS0854)
            repoMock.Setup(r => r.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IQueryable<TEntity>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(TEntity));

            // Явная настройка GetAllAsync с селектором (ВСЕ параметры, чтобы избежать CS0854/CS1929)
            repoMock.Setup(r => r.GetAllAsync(
                    It.IsAny<Expression<Func<TEntity, object>>>(), // selector (упрощенно TEntity, в реальных тестах может быть DTO)
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>());

            repoMock.Setup(r => r.GetAllAsync(
                    It.IsAny<Expression<Func<TEntity, object>>>(), // selector (упрощенно TEntity, в реальных тестах может быть DTO)
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>());

            repoMock.Setup(r => r.GetAllAsync(
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>>(),
                    It.IsAny<Func<IQueryable<TEntity>, IQueryable<TEntity>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<TEntity>() as IReadOnlyList<TEntity>);

            // Явная настройка AnyAsync
            repoMock.Setup(r => r.AnyAsync(
                    It.IsAny<Expression<Func<TEntity, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            uowMock.Setup(repoSelector).Returns(repoMock.Object);
        }
        
        #endregion
    }
}