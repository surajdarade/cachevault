namespace CacheVault.Server.Replication;

public sealed class ReplicaSynchronizationStatus
{
    private readonly object _gate = new();

    private bool _connected;
    private string _replicationId = "?";
    private long _replicationOffset = -1;

    public bool Connected
    {
        get
        {
            lock (_gate)
            {
                return _connected;
            }
        }
    }

    public string ReplicationId
    {
        get
        {
            lock (_gate)
            {
                return _replicationId;
            }
        }
    }

    public long ReplicationOffset
    {
        get
        {
            lock (_gate)
            {
                return _replicationOffset;
            }
        }
    }

    public void ConnectedToMaster(
        string replicationId,
        long replicationOffset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            replicationId);

        lock (_gate)
        {
            _connected = true;
            _replicationId = replicationId;
            _replicationOffset = replicationOffset;
        }
    }

    public void UpdateOffset(
        long replicationOffset)
    {
        lock (_gate)
        {
            _replicationOffset = replicationOffset;
        }
    }

    public void Disconnected()
    {
        lock (_gate)
        {
            _connected = false;
        }
    }
}
