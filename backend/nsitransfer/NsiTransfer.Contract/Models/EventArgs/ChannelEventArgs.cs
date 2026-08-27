using System.Threading.Channels;

namespace NsiTransfer.Contract.Models.EventArgs;

public class ChannelEventArgs : System.EventArgs
{
    public Guid SendingId { get; set; }
    public Channel<SyncEvent> Channel { get; set; }
}
