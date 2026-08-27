using System.Net;

namespace NsiTransfer.Contract.Models.Common;

//public sealed class HttpResult<T>
//{
//    public int StatusCode { get; }
//    public T? Data { get; }
//    public string? ErrorMessage { get; }
//    public bool IsSuccess => StatusCode >= (int)HttpStatusCode.OK && StatusCode <= (int)HttpStatusCode.Ambiguous;

//    private HttpResult(int statusCode, T? data, string? errorMessage)
//    {
//        StatusCode = statusCode;
//        Data = data;
//        ErrorMessage = errorMessage;
//    }

//    public static HttpResult<T> Success(T data, int statusCode = (int)HttpStatusCode.OK)
//        => new(statusCode, data, null);

//    public static HttpResult<T> Failure(int statusCode, string? errorMessage)
//        => new(statusCode, default, errorMessage);

//    public static implicit operator HttpResult<T>(T data) 
//        => Success(data, (int)HttpStatusCode.OK);

//    public static implicit operator HttpResult<T>((T data, int statusCode) param) 
//        => Success(param.data, param.statusCode);

//    public static implicit operator HttpResult<T>((int statusCode, string? errorMessage) param) 
//        => Failure(param.statusCode, param.errorMessage);
//}
