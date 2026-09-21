using NsiTransfer.DAL.Db.Entities;

namespace NsiTransfer.DAL.Interfaces.Db;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<Sending> Sendings { get; }
    IGenericRepository<TargetReferenceNode> TargetReferenceNodes { get; }
    IGenericRepository<Message> Messages { get; }
    IGenericRepository<MessageObject> MessageObjects { get; }
    IGenericRepository<PolynomObject> PolynomObjects { get; }
    IGenericRepository<PolynomObjectFailure> PolynomObjectFailures { get; }
    IGenericRepository<ClassificationGroupCodeMax> ClassificationGroupCodeMaxes { get; }
    IGenericRepository<MessageFailure> MessageFailures { get; }
    IGenericRepository<MessagePublishingResult> PublishingResults { get; }

    IGenericRepository<EmailMessage> EmailMessages { get; }
    IGenericRepository<EmailFailure> EmailFailures { get; }
    IGenericRepository<EmailRecipient> EmailRecipients { get; }


    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

