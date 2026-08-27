using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners.Container;

namespace NsiTransfer.Contract.Models.Ascon;

public class PropertyOwnerResponseCustom : PropertyOwnerContainerCustom, IPropertyOwnerResponseCustom, IPropertyOwnerContainerCustom, IPropertyValueContainer, IPropertyDefinitionContainer, IMeasureEntityContainer
{
    public IPropertyOwnerRef PropertyOwner { get; set; }
}
