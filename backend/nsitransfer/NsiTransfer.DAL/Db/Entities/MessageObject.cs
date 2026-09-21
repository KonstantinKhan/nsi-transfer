using Ascon.Polynom.Web.Api.Data.Interfaces.Enums;

namespace NsiTransfer.DAL.Db.Entities;

/// <summary>
/// Один объект отправления отдельной строкой (нормализованная копия элемента массива Message.SerializedMessage).
/// Решает проблему работы с большими отправлениями в pgAdmin (десятки тысяч объектов в одном jsonb-массиве)
/// и поиска конкретного объекта по json-запросу (вместо поиска по всему массиву целиком).
/// Заполняется только для НОВЫХ сообщений (без миграции ранее накопленных Message.SerializedMessage).
/// Не используется при публикации в RabbitMQ — источник для брокера остаётся Message.SerializedMessage.
/// </summary>
public class MessageObject
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public int PolynomObjectId { get; set; }
    public IdentifiableObjectType PolynomTypeId { get; set; }
    public string Name { get; set; }
    public string SerializedObject { get; set; }

    public Message Message { get; set; }
}
