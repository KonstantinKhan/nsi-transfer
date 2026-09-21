using Microsoft.EntityFrameworkCore.Storage;
using NsiTransfer.DAL.Db.Context;
using NsiTransfer.DAL.Db.Entities;
using NsiTransfer.DAL.Interfaces.Db;

namespace NsiTransfer.DAL.Repositories.Db;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IGenericRepository<Sending>? _sendings;
    private IGenericRepository<TargetReferenceNode>? _targetReferenceNodes;
    private IGenericRepository<Message>? _messages;
    private IGenericRepository<MessageObject>? _messageObjects;
    private IGenericRepository<PolynomObject>? _polynomObjects;
    private IGenericRepository<PolynomObjectFailure>? _polynomObjectFailures;
    private IGenericRepository<ClassificationGroupCodeMax>? _classificationGroupCodeMaxes;
    private IGenericRepository<MessageFailure>? _messageFailures;
    private IGenericRepository<MessagePublishingResult>? _publishingResult;

    private IGenericRepository<EmailMessage>? _emailMessages;
    private IGenericRepository<EmailFailure>? _emailFailures;
    private IGenericRepository<EmailRecipient>? _emailRecipients;

    private IDbContextTransaction? _transaction;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<Sending> Sendings => _sendings ??= new GenericRepository<Sending>(_context);

    public IGenericRepository<TargetReferenceNode> TargetReferenceNodes => _targetReferenceNodes ??= new GenericRepository<TargetReferenceNode>(_context);

    public IGenericRepository<Message> Messages => _messages ??= new GenericRepository<Message>(_context);

    public IGenericRepository<MessageObject> MessageObjects => _messageObjects ??= new GenericRepository<MessageObject>(_context);

    public IGenericRepository<PolynomObject> PolynomObjects => _polynomObjects ??= new GenericRepository<PolynomObject>(_context);

    public IGenericRepository<PolynomObjectFailure> PolynomObjectFailures => _polynomObjectFailures ??= new GenericRepository<PolynomObjectFailure>(_context);

    public IGenericRepository<ClassificationGroupCodeMax> ClassificationGroupCodeMaxes => _classificationGroupCodeMaxes ??= new GenericRepository<ClassificationGroupCodeMax>(_context);

    public IGenericRepository<MessageFailure> MessageFailures => _messageFailures ??= new GenericRepository<MessageFailure>(_context);

    public IGenericRepository<MessagePublishingResult> PublishingResults => _publishingResult ??= new GenericRepository<MessagePublishingResult>(_context);

    public IGenericRepository<EmailMessage> EmailMessages => _emailMessages ??= new GenericRepository<EmailMessage>(_context);

    public IGenericRepository<EmailFailure> EmailFailures => _emailFailures ??= new GenericRepository<EmailFailure>(_context);

    public IGenericRepository<EmailRecipient> EmailRecipients => _emailRecipients ??= new GenericRepository<EmailRecipient>(_context);


    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _transaction?.CommitAsync(cancellationToken)!;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transaction?.Dispose();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        await _transaction?.RollbackAsync(cancellationToken)!;
        _transaction?.Dispose();
        _transaction = null;
    }

    public void Dispose()
    {
        _context.Dispose();
        _transaction?.Dispose();
    }
}
