using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners.Container;

namespace NsiTransfer.Contract.Models.Ascon;

public interface IPropertyOwnerResponseCustom : IPropertyOwnerContainerCustom, IPropertyValueContainer, IPropertyDefinitionContainer, IMeasureEntityContainer
{
    IPropertyOwnerRef PropertyOwner { get; set; }
}
