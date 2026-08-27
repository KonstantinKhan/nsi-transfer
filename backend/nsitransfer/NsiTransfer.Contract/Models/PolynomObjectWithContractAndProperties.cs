using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models;

public class PolynomObjectWithContractAndProperties : PolynomObjectWithName
{
    public List<PropertiesContract> AllContracts { get; set; }
    public IAblePropertyDefinitions Definitions { get; set; }
    public IAbleMeasureEntities MeasureEntities { get; set; }
    public IAbleMeasureUnits MeasureUnits { get; set; }
    public PropertyOwnerShort PropertyOwnerShort { get; set; }
    public IAblePropertyValues PropertyValues { get; set; }
}
