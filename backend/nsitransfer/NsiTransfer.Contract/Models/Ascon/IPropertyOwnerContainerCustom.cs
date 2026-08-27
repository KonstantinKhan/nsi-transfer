using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners.Container;

namespace NsiTransfer.Contract.Models.Ascon;

public interface IPropertyOwnerContainerCustom : IPropertyValueContainer, IPropertyDefinitionContainer, IMeasureEntityContainer
{
    public List<IContractRefCustom> AllContracts { get; set; }
}
