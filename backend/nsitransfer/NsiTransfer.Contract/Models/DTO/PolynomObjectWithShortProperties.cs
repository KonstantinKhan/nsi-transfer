using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract.Models.DTO;

public class PolynomObjectWithShortProperties : PolynomObjectWithName
{
    public List<PolynomContractWithShortProperties> Contracts { get; set; }
}
