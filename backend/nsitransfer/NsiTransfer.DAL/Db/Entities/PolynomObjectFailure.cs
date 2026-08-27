using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.DAL.Db.Entities;

public class PolynomObjectFailure
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public int PolynomObjectId { get; set; }
    public IdentifiableObjectType PolynomTypeId { get; set; }
    public string ObjectName { get; set; }
    public PolynomObjectFailureTypeEnum FailureType { get; set; }
    public DateTime FailedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public Message Message { get; set; }
}
