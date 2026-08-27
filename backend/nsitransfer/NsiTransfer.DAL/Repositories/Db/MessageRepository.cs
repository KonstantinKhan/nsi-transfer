using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NsiTransfer.Contract.Models.Common;
using NsiTransfer.DAL.Db.Context;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;

namespace NsiTransfer.DAL.Repositories.Db;

/// <summary>
/// Репозиторий для работы с сущностью Message
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<MessageRepository> _logger;

    public MessageRepository(AppDbContext dbContext, ILogger<MessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<long>> CreateAsync(Message message, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.Messages.Add(message);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Message entity created successfully with ID: {MessageId}", message.Id);
            return message.Id;
        }
        catch (DbUpdateException ex)
        {
            var errorMessage = $"Ошибка при сохранении сущности Message в БД: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            var errorMessage = $"Операция создания Message была отменена: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Неожиданная ошибка при создании Message: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
    }

    public async Task<Result<Message>> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await _dbContext.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (message == null)
            {
                var errorMessage = $"Message с ID {id} не найден в БД";
                _logger.LogWarning(errorMessage);
                return errorMessage;
            }

            return message;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            var errorMessage = $"Операция получения Message была отменена: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Ошибка при получении Message из БД: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
    }

    public async Task<Result> UpdateAsync(Message message, CancellationToken cancellationToken = default)
    {
        try
        {
            _dbContext.Messages.Update(message);
            await _dbContext.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Message entity updated successfully with ID: {MessageId}", message.Id);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            var errorMessage = $"Ошибка при обновлении сущности Message в БД: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            var errorMessage = $"Операция обновления Message была отменена: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Неожиданная ошибка при обновлении Message: {ex.Message}";
            _logger.LogError(ex, errorMessage);
            return errorMessage;
        }
    }
}

