namespace NsiTransfer.Contract.Models.Common;

public class PolynomObjectWithName : PolynomObject
{
    public PolynomObjectWithName() { }
    public PolynomObjectWithName(int objectId, int typeId, string name) : base(objectId, typeId) => Name = name;
    public string Name { get; set; }
}
