namespace NsiTransfer.Contract.Models;

public class PolynomAuthTokenModel
{
    public string AccessToken { get; set; }

    public int ExpiresIn { get; set;}
    private DateTime _obtainedAt_Utc = DateTime.UtcNow;
    public bool IsExpired => DateTime.UtcNow >= _obtainedAt_Utc.AddSeconds(ExpiresIn - 30); 
    // Считаем токен просроченным за 30 секунд до фактического истечения срока действия

    public string RefreshToken { get; set; }
    public string TokenType { get; set; }
}
