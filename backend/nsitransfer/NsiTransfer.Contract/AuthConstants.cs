namespace NsiTransfer.Contract;

public static class AuthConstants
{
    public const string AuthorizationHeaderName = "Authorization";

    public const string AccessToken = "AccessToken";

    public const string BearerTypeName = "Bearer";

    public const string AuthenticationSchemeName = "SimpleBearerAuth";


    public const string AccessTokenCookieName = "auth_access_token";

    public const string RefreshTokenCookieName = "auth_refresh_token";

    public const string ExpiresAtCookieName = "auth_expires_at";
}
