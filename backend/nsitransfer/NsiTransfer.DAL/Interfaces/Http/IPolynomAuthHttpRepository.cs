using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Security;
using Ascon.Polynom.Web.Api.Data.Requests.Login;
using Ascon.Polynom.Web.Api.Data.Responses;
using NsiTransfer.Contract.Models;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.DAL.Interfaces.Http;

public interface IPolynomAuthHttpRepository
{
    Task<Result<List<StorageDefinitionResponse>>> GetStoragesAsync(CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> SignInAsync(SignInRequestCustom signInRequestCustom, CancellationToken cancellationToken = default);
    Task<Result> SignOutAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshTokenOrSignInAsync(string refreshToken, SignInRequestCustom signInRequestCustom, CancellationToken cancellationToken = default);

    Task<Result<IUser>> GetUserInfo(string accessToken, CancellationToken cancellationToken = default);
}