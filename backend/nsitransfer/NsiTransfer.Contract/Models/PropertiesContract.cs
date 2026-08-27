using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models;

public class PropertiesContract : PolynomObjectWithName
{
    public Guid Id { get; set; }
    public string AbsoluteCode { get; set; }
    public string Code { get; set; }
    public bool IsSystemObject { get; set; }
    public List<ContractProperty> Properties { get; set; }
}
