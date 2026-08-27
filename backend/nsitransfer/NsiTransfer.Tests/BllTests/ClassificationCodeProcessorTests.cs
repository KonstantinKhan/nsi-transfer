using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using Ascon.Polynom.Web.Api.Data.Responses;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NsiTransfer.BLL.Interfaces.Services;
using NsiTransfer.BLL.Services;
using NsiTransfer.Contract.ConfigModels;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.Contract.Models.DTO;
using NsiTransfer.Contract.Models.Enums;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;
using System.Linq.Expressions;

namespace NsiTransfer.Tests.BllTests // Рекомендуется добавить namespace
{
    [TestFixture] // 2. Атрибут класса в NUnit
    public class ClassificationCodeProcessorTests
    {
        private Mock<IPolynomApiService> _polynomApiServiceMock = null!;
        private Mock<IUnitOfWork> _unitOfWorkMock = null!;
        private Mock<IOptionsMonitor<PolynomApiSyncOptions>> _optionsMock = null!;
        //private Mock<ILogger<ClassificationCodeProcessor>> _loggerMock = null!;
        private ClassificationCodeProcessor _processor = null!;

        [SetUp] // 3. Атрибут метода инициализации в NUnit (вместо конструктора)
        public void Setup()
        {
            _polynomApiServiceMock = new Mock<IPolynomApiService>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            //_loggerMock = new Mock<ILogger<ClassificationCodeProcessor>>();
            
            var options = new PolynomApiSyncOptions
            {
                ConceptNameForClassificationData = "ConceptA",
                ClassificationCodePropertyName = "CodeProp",
                MinCodePropertyName = "Min",
                MaxCodePropertyName = "Max",
                OwnContractName = "Own"
            };
            _optionsMock = new Mock<IOptionsMonitor<PolynomApiSyncOptions>>();
            _optionsMock.Setup(o => o.CurrentValue).Returns(options);

            // Настройка моков репозиториев
            var failureRepoMock = new Mock<IGenericRepository<PolynomObjectFailure>>();
            
            // 4. ИСПРАВЛЕНИЕ ОШИБОК CS1929 и CS0854:
            // В деревьях выражений (Expression) Moq нельзя полагаться на опциональные аргументы C#.
            // Необходимо явно указать It.IsAny<T>() для КАЖДОГО параметра метода GetAllAsync.
            failureRepoMock.Setup(r => r.GetAllAsync(
                    It.IsAny<Expression<Func<PolynomObjectFailure, bool>>>(),
                    It.IsAny<Func<IQueryable<PolynomObjectFailure>, IOrderedQueryable<PolynomObjectFailure>>>(),
                    It.IsAny<Func<IQueryable<PolynomObjectFailure>, IQueryable<PolynomObjectFailure>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PolynomObjectFailure>() as IReadOnlyList<PolynomObjectFailure>);
                
            failureRepoMock.Setup(r => r.AddAsync(It.IsAny<PolynomObjectFailure>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PolynomObjectFailure f, CancellationToken ct) => f);
                
            _unitOfWorkMock.Setup(u => u.PolynomObjectFailures).Returns(failureRepoMock.Object);

            _processor = new ClassificationCodeProcessor(
                _polynomApiServiceMock.Object,
                _unitOfWorkMock.Object,
                _optionsMock.Object,
                NullLogger<ClassificationCodeProcessor>.Instance);
        }

        [Test] // 5. Атрибут теста в NUnit (вместо [Fact])
        public async Task ProcessAsync_ObjectAlreadyHasCode_ShouldReturnLogWithoutApiCalls()
        {
            // Arrange
            var model = CreateModelWithExistingCode("123");
            var message = new Message { Id = 1 };

            // Act
            var result = await _processor.ProcessAsync(model, message, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data!.ClassificationCode.Should().Be("123"); // Добавлен ! для подавления предупреждения о null, так как IsSuccess == true
            _polynomApiServiceMock.Verify(p => p.GetParentGroupsWithProperties(It.IsAny<int>(), It.IsAny<IdentifiableObjectType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ProcessAsync_NoParentGroups_ShouldFailAndSaveToDb()
        {
            // Arrange
            var model = CreateModelWithoutCode();
            var message = new Message { Id = 1 };

            _polynomApiServiceMock.Setup(p => p.GetParentGroupsWithProperties(It.IsAny<int>(), It.IsAny<IdentifiableObjectType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<PolynomObjectWithShortProperties>>.Success(new List<PolynomObjectWithShortProperties>())); // 0 групп

            // Act
            var result = await _processor.ProcessAsync(model, message, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            _unitOfWorkMock.Verify(u => u.PolynomObjectFailures.AddAsync(
                It.Is<PolynomObjectFailure>(f => f.FailureType == PolynomObjectFailureTypeEnum.NoGroupsAtAll), 
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task ProcessAsync_ValidParentGroup_ShouldGenerateAndSetCode()
        {
            // Arrange
            var model = CreateModelWithoutCode();
            var message = new Message { Id = 1 };
            
            var parentGroup = CreateParentGroup(min: "100", max: "999");
            _polynomApiServiceMock.Setup(p => p.GetParentGroupsWithProperties(It.IsAny<int>(), It.IsAny<IdentifiableObjectType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<PolynomObjectWithShortProperties>>.Success(new List<PolynomObjectWithShortProperties> { parentGroup }));

            // 6. ИСПРАВЛЕНИЕ ОШИБКИ CS8620:
            // Интерфейс, судя по ошибке, возвращает Task<Result<string?>> (nullable string).
            // Явно указываем string? в дженерике Result, чтобы типы совпадали.
            _polynomApiServiceMock.Setup(p => p.GetLastClassificationCodeInGroup(It.IsAny<int>(), It.IsAny<IdentifiableObjectType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string?>.Success("105")); 

            _polynomApiServiceMock.Setup(p => p.UpdateClassificationCodeAsync(It.IsAny<PolynomObjectWithShortProperties>(), It.IsAny<PolynomContractWithShortProperties>(), It.IsAny<PolynomShortProperty>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<SetPropertyValuesResponse>.Success(new SetPropertyValuesResponse())); 

            // Act
            var result = await _processor.ProcessAsync(model, message, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Data!.ClassificationCode.Should().Be("106"); // 105 + 1
            model.Contracts[0].Properties[0].Value.Should().Be("106"); // Код обновился в модели
        }
        
        [Test]
        public async Task ProcessAsync_NewCodeExceedsMax_ShouldThrowException()
        {
            // Arrange
            var model = CreateModelWithoutCode();
            var message = new Message { Id = 1 };
            
            var parentGroup = CreateParentGroup(min: "100", max: "105");
            _polynomApiServiceMock.Setup(p => p.GetParentGroupsWithProperties(It.IsAny<int>(), It.IsAny<IdentifiableObjectType>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<List<PolynomObjectWithShortProperties>>.Success(new List<PolynomObjectWithShortProperties> { parentGroup }));

            _polynomApiServiceMock.Setup(p => p.GetLastClassificationCodeInGroup(It.IsAny<int>(), It.IsAny<IdentifiableObjectType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string?>.Success("105")); 

            // Act & Assert
            // 8. ИСПРАВЛЕНИЕ СИНТАКСИСА NUNIT для асинхронных исключений:
            // В NUnit используется Assert.That с делегатом и ограничением Throws
            Assert.That(async () => await _processor.ProcessAsync(model, message, CancellationToken.None), 
                Throws.InstanceOf<InvalidOperationException>().And.Message.Contains("превышает максимально допустимый"));
        }

        #region Helpers
        private PolynomObjectWithShortProperties CreateModelWithExistingCode(string code)
        {
            var model = CreateModelWithoutCode();
            model.Contracts[0].Properties[0].Value = code;
            return model;
        }

        private PolynomObjectWithShortProperties CreateModelWithoutCode()
        {
            return new PolynomObjectWithShortProperties
            {
                ObjectId = 1,
                TypeId = (IdentifiableObjectType)1,
                Name = "TestObject",
                Contracts = new List<PolynomContractWithShortProperties>
                {
                    new()
                    {
                        Name = "ConceptA",
                        Properties = new List<PolynomShortProperty>
                        {
                            new() { Name = "CodeProp", Value = null }
                        }
                    }
                }
            };
        }

        private PolynomObjectWithShortProperties CreateParentGroup(string min, string max)
        {
            return new PolynomObjectWithShortProperties
            {
                ObjectId = 99,
                TypeId = (IdentifiableObjectType)2,
                Name = "ParentGroup",
                Contracts = new List<PolynomContractWithShortProperties>
                {
                    new()
                    {
                        Name = "Own",
                        Properties = new List<PolynomShortProperty>
                        {
                            new() { Name = "Min", Value = min },
                            new() { Name = "Max", Value = max }
                        }
                    }
                }
            };
        }
        #endregion
    }
}