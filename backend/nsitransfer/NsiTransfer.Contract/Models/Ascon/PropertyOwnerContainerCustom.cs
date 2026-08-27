using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners.Container;
using Ascon.Polynom.Web.Api.Data.Models.PropertyOwners.Container;

namespace NsiTransfer.Contract.Models.Ascon;

public class PropertyOwnerContainerCustom : PropertyValueContainer, IPropertyOwnerContainerCustom, IPropertyValueContainer, IPropertyDefinitionContainer, IMeasureEntityContainer
{
    public List<IContractRefCustom> AllContracts { get; set; } = new List<IContractRefCustom>();
}
