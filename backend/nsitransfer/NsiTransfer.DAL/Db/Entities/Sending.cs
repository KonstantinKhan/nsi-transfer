using NsiTransfer.Contract.Models.Enums;

namespace NsiTransfer.DAL.Db.Entities;
public class Sending
{
    // Единственный источник правды о том, что считается "активной" синхронизацией.
    // Если добавишь новый промежуточный статус (например, Paused) — не забудь внести его сюда,
    // иначе SetStatus() сочтёт его терминальным, снимет ActiveMarker и проставит EndedAt.
    private static readonly HashSet<SendingStatusEnum> ActiveStatuses = new()
    {
        SendingStatusEnum.Initiated,
        SendingStatusEnum.Pending
    };

    public Guid Id { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? EndedAt { get; private set; }
    public string InitiatorName { get; set; }
    public SendingStatusEnum StatusId { get; private set; }

    // true, пока синхронизация активна; null, когда завершена (терминальный статус).
    // Уникальный индекс по этому полю на уровне БД не даёт создать вторую активную запись.
    public bool? ActiveMarker { get; private set; }

    public int TargetReferenceNodeId { get; set; }
    public TargetReferenceNode TargetReferenceNode { get; set; }

    public ICollection<Message> Messages { get; set; }

    /// <summary>
    /// Единственный способ поменять статус. Атомарно синхронизирует StatusId, ActiveMarker и EndedAt,
    /// чтобы их нельзя было развести по разным вызовам SaveChanges или забыть один из них в новом месте кода.
    /// </summary>
    /// <param name="occurredAt">
    /// Момент времени для EndedAt при переходе в терминальный статус. Если не передан — берётся
    /// DateTime.UtcNow на момент вызова. Передавай явно, когда в другом месте уже зафиксировано
    /// точное время события (например, FailedAt у MessageFailure), чтобы оба поля указывали на один момент.
    /// </param>
    public void SetStatus(SendingStatusEnum status, DateTime? occurredAt = null)
    {
        StatusId = status;

        if (ActiveStatuses.Contains(status))
        {
            ActiveMarker = true;
            EndedAt = null;
        }
        else
        {
            ActiveMarker = null;
            EndedAt = occurredAt ?? DateTime.UtcNow;
        }
    }
}
