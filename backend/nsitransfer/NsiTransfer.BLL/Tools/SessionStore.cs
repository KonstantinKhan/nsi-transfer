using System.Collections.Concurrent;
using NsiTransfer.Contract.Models;

namespace NsiTransfer.BLL.Tools;

public class SessionStore
{
    private readonly ConcurrentDictionary<UserCredentialsKey, PolynomAuthTokenModel> _tokens = new();

    public void Set(UserCredentialsKey credentials, PolynomAuthTokenModel token)
    {
        _tokens[credentials] = token;
    }

    public bool TryGet(UserCredentialsKey credentials, out PolynomAuthTokenModel token)
    {
        return _tokens.TryGetValue(credentials, out token);
    }

    public bool TryRemove(UserCredentialsKey credentials)
    {
        return _tokens.TryRemove(credentials, out _);
    }
}

public sealed record UserCredentialsKey(string Username, string Password);
