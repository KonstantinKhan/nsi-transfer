using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Properties;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using System.Text.Json.Serialization;

namespace NsiTransfer.Contract.Models.Ascon;

[JsonDerivedType(typeof(ContractRefCustom))]
public interface IContractRefCustom : IContractBase, INamedObject, IIdentifiableObject, IEquatable<ObjectIdentifier>, IEquatable<IIdentifiableObject>, IHaveName, IHaveId, IHaveAbsoluteCode, IHaveCode, IHaveDescription, ICanBeSystemObject, IAccessControlObject, IHaveWriteAccess
{
    List<IContractPropertySourceRef> Properties { get; set; }

    bool CanUnassign { get; set; }

    INamedObject? SuperConcept { get; set; }

    bool OwnPropertyValues { get; set; }
}
