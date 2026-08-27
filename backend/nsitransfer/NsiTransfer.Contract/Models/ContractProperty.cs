using Ascon.Polynom.Web.Api.Data.Interfaces.Models.PropertyOwners;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models;

public class ContractProperty : PolynomObjectWithName
{
    public Guid Id { get; set; }
    public string AbsoluteCode { get; set; }

    /// <summary>
    /// Позиция по порядку отображения в списке свойств
    /// </summary>
    public int Position { get; set; }
    public PolynomObject? DefaultMeasureUnit { get; set; }
    public PolynomObject? DefaultPropertyValue { get; set; }
    public IPropertySourceRef PropertySource { get; set; }
}
