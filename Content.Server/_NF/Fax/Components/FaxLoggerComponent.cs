using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Server._NF.FaxLogger.Components;

[RegisterComponent]
public sealed partial class FaxLoggerComponent : Component
{
    [DataField]
    public Queue<FaxRecord> FaxLog = new();
}


[DataDefinition, Serializable]
public readonly partial record struct FaxRecord(
    [property: DataField, ViewVariables(VVAccess.ReadWrite)]
    TimeSpan AccessTime,
    [property: DataField, ViewVariables(VVAccess.ReadWrite)]
    string Sender,
    [property: DataField, ViewVariables(VVAccess.ReadWrite)]
    string Reciever)

{
    public FaxRecord() : this(TimeSpan.Zero, string.Empty, string.Empty)
    {
    }

}

