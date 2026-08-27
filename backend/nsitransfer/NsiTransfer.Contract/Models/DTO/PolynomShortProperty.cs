using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;
using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models.DTO;

public class PolynomShortProperty
{
    public string Name { get; set; }
    public string? Value { get; set; }

    public string? MeasureUnitDesignation { get; set; }
    public string? MeasureUnitName { get; set; }

    public PolynomObject Definition { get; set; }

    public PropertyType? PropertyTypeId { get; set; }
    public string? PropertyTypeName { get; set; }

    public string? Description { get; set; }
    public string? ClassifId { get; set; }
}
