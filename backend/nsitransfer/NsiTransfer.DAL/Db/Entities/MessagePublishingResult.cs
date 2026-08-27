using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.DAL.Db.Entities;

public class MessagePublishingResult
{
    public RabbitMqPublishingResultEnum Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}