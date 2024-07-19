using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class FaxProbeUiState : BoundUserInterfaceState
{
    /// <summary>
    /// The list of probed network devices
    /// </summary>
    public List<PulledFaxLog> PulledLogs;

    public FaxProbeUiState(List<PulledFaxLog> pulledLogs)
    {
        PulledLogs = pulledLogs;
    }
}

[Serializable, NetSerializable, DataRecord]
public sealed class PulledFaxLog
{
    public readonly TimeSpan Time;
    public readonly string Sender;
    public readonly string Reciever;

    public PulledFaxLog(TimeSpan time, string sender, string reciever)
    {
        Time = time;
        Sender = sender;
        Reciever = reciever;
    }
}
