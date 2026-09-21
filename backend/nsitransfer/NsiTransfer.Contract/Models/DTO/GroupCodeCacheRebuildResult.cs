namespace NsiTransfer.Contract.Models.DTO;

/// <summary>
/// Итог полной переиндексации персистентного кеша последних кодов классификатора по группам.
/// </summary>
public class GroupCodeCacheRebuildResult
{
    public int GroupsIndexed { get; set; }
    public int GroupsSkippedNotAClassificationGroup { get; set; }
    public int GroupsWithErrors { get; set; }
    public List<string> Errors { get; set; } = [];
}
