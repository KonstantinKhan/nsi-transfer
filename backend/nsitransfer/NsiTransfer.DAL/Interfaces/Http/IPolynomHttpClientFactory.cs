namespace NsiTransfer.DAL.Interfaces.Http;

internal interface IPolynomHttpClientFactory
{
    IPolynomHttpClient Create(string clientKey);
}
