using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;

namespace NsiTransfer.DAL.Db.Entities;

public class PolynomObject
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string Name { get; set; }
    public string ClassificationCode { get; set; }
    public long PolynomObjectId { get; set; }
    public IdentifiableObjectType PolynomTypeId { get; set; }

    public Message Message { get; set; }
}
