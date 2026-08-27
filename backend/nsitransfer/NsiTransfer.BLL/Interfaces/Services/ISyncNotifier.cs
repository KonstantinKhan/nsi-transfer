using NsiTransfer.Contract.Models.EventArgs;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace NsiTransfer.BLL.Interfaces.Services;


public interface ISyncNotifier
{
    void InitializeStream(Guid sendingId);

    bool TryGetReader(Guid sendingId, out ChannelReader<SyncEvent> reader);
    
    IAsyncEnumerable<SyncEvent> StreamEvents(Guid sendingId, [EnumeratorCancellation] CancellationToken cancellationToken);

    Task NotifyAsync(Guid sendingId, SyncEvent syncEvent, CancellationToken cancellationToken);
    
    void Complete(Guid sendingId);




    event SyncEventProducer? SyncEventsEvent;
}

public delegate void SyncEventProducer(object sender, SyncEventEventArgs e);
