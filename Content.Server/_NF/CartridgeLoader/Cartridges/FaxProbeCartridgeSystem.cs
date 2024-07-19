using Content.Shared._NF.FaxLogger.Components;
using Content.Shared.Audio;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed class FaxProbeCartridgeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FaxProbeCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<FaxProbeCartridgeComponent, CartridgeAfterInteractEvent>(AfterInteract);
    }

    /// <summary>
    /// The <see cref="CartridgeAfterInteractEvent" /> gets relayed to this system if the cartridge loader is running
    /// the FaxProbe program and someone clicks on something with it. <br/>
    /// <br/>
    /// Updates the program's list of logs with those from the device.
    /// </summary>
    private void AfterInteract(Entity<FaxProbeCartridgeComponent> ent, ref CartridgeAfterInteractEvent args)
    {
        if (args.InteractEvent.Handled || !args.InteractEvent.CanReach || args.InteractEvent.Target is not { } target)
            return;

        if (!TryComp(target, out FaxLoggerComponent? faxLoggerComponent))
            return;

        //Play scanning sound with slightly randomized pitch
        _audioSystem.PlayEntity(ent.Comp.SoundScan, args.InteractEvent.User, target, AudioHelpers.WithVariation(0.25f, _random));
        _popupSystem.PopupCursor(Loc.GetString("log-probe-scan", ("device", target)), args.InteractEvent.User);

        ent.Comp.PulledFaxLogs.Clear();

        foreach (var accessRecord in faxLoggerComponent.FaxLog)
        {
            var log = new PulledFaxLog(
                accessRecord.AccessTime,
                accessRecord.Sender,
                accessRecord.Reciever
            );

            ent.Comp.PulledFaxLogs.Add(log);
        }

        UpdateUiState(ent, args.Loader);
    }

    /// <summary>
    /// This gets called when the ui fragment needs to be updated for the first time after activating
    /// </summary>
    private void OnUiReady(Entity<FaxProbeCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader);
    }

    private void UpdateUiState(Entity<FaxProbeCartridgeComponent> ent, EntityUid loaderUid)
    {
        var state = new FaxProbeUiState(ent.Comp.PulledFaxLogs);
        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }
}
