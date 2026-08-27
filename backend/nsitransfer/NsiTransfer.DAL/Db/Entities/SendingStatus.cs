namespace NsiTransfer.DAL.Db.Entities;

public class SendingStatus
{
    public Contract.Models.Enums.SendingStatusEnum Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}
