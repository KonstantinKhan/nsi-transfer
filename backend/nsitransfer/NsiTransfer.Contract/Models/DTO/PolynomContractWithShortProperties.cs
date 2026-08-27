using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models.DTO;

public class PolynomContractWithShortProperties : PolynomObjectWithName
{
    public List<PolynomShortProperty> Properties { get; set; }
}
