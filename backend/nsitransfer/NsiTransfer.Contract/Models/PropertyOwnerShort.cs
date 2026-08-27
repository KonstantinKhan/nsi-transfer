using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models;

public class PropertyOwnerShort : PolynomObject
{
    public PropertyOwnerShort(string id, int objectId, IdentifiableObjectType typeId) : base(objectId, typeId) => Id = new Guid(id);
    public Guid Id { get; set; }
}
