﻿using System.Text;
using Content.Server._NF.Smuggling.Components;
using Content.Server.Administration.Logs;
using Content.Server.Fax;
using Content.Server.Paper;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shipyard.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Fax.Components;
using Content.Shared.Database;
using Content.Shared.Radio;
using Content.Shared.Shuttles.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Emag.Systems;

namespace Content.Server._NF.Smuggling;

public sealed class DeadDropSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!;
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ShipyardSystem _shipyard = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeadDropComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DeadDropComponent, GetVerbsEvent<InteractionVerb>>(AddSearchVerb);
        SubscribeLocalEvent<DeadDropComponent, GotDropPrintedEvent>(OnDeadDropPrinterUsed);
    }

    private void OnStartup(EntityUid uid, DeadDropComponent component, ComponentStartup _)
    {
        //set up the timing of the first activation
        component.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(component.MinimumCoolDown, component.MaximumCoolDown));
    }

    private void AddSearchVerb(EntityUid uid, DeadDropComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null || _timing.CurTime < component.NextDrop || HasComp<FaxMachineComponent>(uid))
            return;

        //here we build our dynamic verb. Using the object's sprite for now to make it more dynamic for the moment.
        InteractionVerb searchVerb = new()
        {
            IconEntity = GetNetEntity(uid),
            Act = () => SendDeadDrop(uid, component, args.User, args.Hands),
            Text = Loc.GetString("deaddrop-search-text"),
            Priority = 3
        };

        args.Verbs.Add(searchVerb);
    }

    //spawning the dead drop.
    private void SendDeadDrop(EntityUid uid, DeadDropComponent component, EntityUid user, HandsComponent hands)
    {
        //simple check to make sure we dont allow multiple activations from a desynced verb window.
        if (_timing.CurTime < component.NextDrop)
            return;

        //relying entirely on shipyard capabilities, including using the shipyard map to spawn the items and ftl to bring em in
        if (_shipyard.ShipyardMap is not MapId shipyardMap)
            return;

        var options = new MapLoadOptions
        {
            LoadMap = false,
        };

        //load whatever grid was specified on the component, either a special dead drop or default
        if (!_map.TryLoad(shipyardMap, component.DropGrid, out var gridUids, options))
            return;

        //setup the radar properties
        _shuttle.SetIFFColor(gridUids[0], component.Color);
        _shuttle.AddIFFFlag(gridUids[0], IFFFlags.HideLabel);

        //this is where we set up all the information that FTL is going to need, including a new null entitiy as a destination target because FTL needs it for reasons?
        //dont ask me im just fulfilling FTL requirements.
        var dropLocation = _random.NextVector2(component.MinimumDistance, component.MaximumDistance);
        var mapId = Transform(user).MapID;
        var mapUid = _mapManager.GetMapEntityId(mapId);

        if (TryComp<ShuttleComponent>(gridUids[0], out var shuttle))
        {
            _shuttle.FTLToCoordinates(gridUids[0], shuttle, new EntityCoordinates(mapUid, dropLocation), 0f, 0f, 35f);
        }

        //tattle on the smuggler here, but obfuscate it a bit if possible to just the grid it was summoned from.
        var channel = _prototypeManager.Index<RadioChannelPrototype>("Nfsd");
        var sender = Transform(user).GridUid ?? uid;

        _radio.SendRadioMessage(sender, Loc.GetString("deaddrop-security-report"), channel, uid);
        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user)} sent a dead drop to {dropLocation.ToString()} from {ToPrettyString(uid)} at {Transform(uid).Coordinates.ToString()}");

        // here we are just building a string for the hint paper so that it looks pretty and RP-like on the paper itself.
        var dropHint = new StringBuilder();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-pretext"));
        dropHint.AppendLine();
        dropHint.AppendLine(dropLocation.ToString());
        dropHint.AppendLine();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-posttext"));

        var paper = EntityManager.SpawnEntity(component.HintPaper, Transform(uid).Coordinates);

        _paper.SetContent(paper, dropHint.ToString());
        _meta.SetEntityName(paper, Loc.GetString("deaddrop-hint-name"));
        _meta.SetEntityDescription(paper, Loc.GetString("deaddrop-hint-desc"));
        _hands.PickupOrDrop(user, paper, handsComp: hands);

        //reset the timer
        component.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(component.MinimumCoolDown, component.MaximumCoolDown));
    }

//Keeping printing faxs and finding them behind posters in different methods to reduce clutter
    private bool FaxDeadDrop(EntityUid uid, DeadDropComponent component)
    {
 //relying entirely on shipyard capabilities, including using the shipyard map to spawn the items and ftl to bring em in
        if (_shipyard.ShipyardMap is not MapId shipyardMap)
            return false;

        var options = new MapLoadOptions
        {
            LoadMap = false,
        };

        //load whatever grid was specified on the component, either a special dead drop or default
        if (!_map.TryLoad(shipyardMap, component.DropGrid, out var gridUids, options))
            return false;

        //setup the radar properties
        _shuttle.SetIFFColor(gridUids[0], component.Color);
        _shuttle.AddIFFFlag(gridUids[0], IFFFlags.HideLabel);

        //this is where we set up all the information that FTL is going to need, including a new null entitiy as a destination target because FTL needs it for reasons?
        //dont ask me im just fulfilling FTL requirements.
        var dropLocation = _random.NextVector2(component.MinimumDistance, component.MaximumDistance);
        var mapId = Transform(uid).MapID;
        var mapUid = _mapManager.GetMapEntityId(mapId);

        if (TryComp<ShuttleComponent>(gridUids[0], out var shuttle))
        {
            _shuttle.FTLToCoordinates(gridUids[0], shuttle, new EntityCoordinates(mapUid, dropLocation), 0f, 0f, 35f);
        }

        //tattle on the smuggler here, more obfuscated than grabbing from a poster. Not sure if that will be confusing for people.
        var channel = _prototypeManager.Index<RadioChannelPrototype>("Nfsd");

        _radio.SendRadioMessage(uid, Loc.GetString("deaddrop-security-report"), channel, uid);

        // here we are just building a string for the hint paper so that it looks pretty and RP-like on the paper itself.
        var dropHint = new StringBuilder();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-pretext"));
        dropHint.AppendLine();
        dropHint.AppendLine(dropLocation.ToString());
        dropHint.AppendLine();
        dropHint.AppendLine(Loc.GetString("deaddrop-hint-posttext"));
        _faxSystem.Receive(
        uid,
        new FaxPrintout(dropHint.ToString(),
        Loc.GetString("deaddrop-hint-name"),
        "Syndicate HQ"));

        _adminLogger.Add(LogType.Action, LogImpact.Medium, $"sent a dead drop to {dropLocation.ToString()} from {ToPrettyString(uid)} at {Transform(uid).Coordinates.ToString()}");
        return true;
    }
    private void OnDeadDropPrinterUsed(EntityUid uid, DeadDropComponent comp, ref GotDropPrintedEvent args)
    {
        args.Handled = FaxDeadDrop(args.TargetUid, comp);
    }

}
