using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;

namespace NsiTransfer.Contract.Models.Common;

public class PolynomObject
{
    public PolynomObject() { }
    public PolynomObject(int objectId, IdentifiableObjectType typeId)
    {
        ObjectId = objectId;
        TypeId = typeId;
    }
    public PolynomObject(int objectId, int typeId)
    {
        ObjectId = objectId;
        TypeId = (IdentifiableObjectType)typeId;
    }

    public int ObjectId { get; set; }
    public IdentifiableObjectType TypeId { get; set; }
}
