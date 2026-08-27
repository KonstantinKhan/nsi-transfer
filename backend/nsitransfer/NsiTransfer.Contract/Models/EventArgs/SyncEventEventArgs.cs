namespace NsiTransfer.Contract.Models.EventArgs;

public class SyncEventEventArgs : System.EventArgs
{
    public Guid SendingId { get; set; }
    public SyncEvent SyncEvent { get; set; }
}
