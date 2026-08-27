using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Base;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.Properties;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using Ascon.Polynom.Web.Api.Data.Models.Properties;

namespace NsiTransfer.Contract.Models.Ascon;

public class ContractRefCustom : ContractBase, IContractRefCustom, IContractBase, INamedObject, IIdentifiableObject, IEquatable<ObjectIdentifier>, IEquatable<IIdentifiableObject>, IHaveName, IHaveId, IHaveAbsoluteCode, IHaveCode, IHaveDescription, ICanBeSystemObject, IAccessControlObject, IHaveWriteAccess
{
    public List<IContractPropertySourceRef> Properties { get; set; } = new List<IContractPropertySourceRef>();

    public bool CanUnassign { get; set; }

    public INamedObject? SuperConcept { get; set; }

    public bool OwnPropertyValues { get; set; } = true;
}
