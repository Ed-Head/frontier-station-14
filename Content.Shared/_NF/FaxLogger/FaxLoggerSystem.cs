using Content.Shared._NF.FaxLogger.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;
using Robust.Shared.GameStates;

namespace Content.Shared._NF.FaxLogger.Systems;

public sealed class FaxLoggerSystem : EntitySystem
{

    [Dependency] private readonly SharedGameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public void TryLog(EntityUid ent, string sender, string reciever, FaxLoggerComponent? comp = null)
    {
        if (!Resolve(ent, ref comp, false))
            return;
        LogFaxSent((ent, comp), sender, reciever);
    }
    public void LogFaxSent(Entity<FaxLoggerComponent> ent, string sender, string reciever)
    {
        var stationTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
        ent.Comp.FaxLog.Enqueue(new FaxRecord(stationTime, sender, reciever));
    }
}
