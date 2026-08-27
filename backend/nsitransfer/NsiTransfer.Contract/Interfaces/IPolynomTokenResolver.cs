using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Interfaces;

public interface IPolynomTokenResolver
{
    Task<Result<string>> ResolveAccessTokenAsync(CancellationToken cancellationToken = default);
}