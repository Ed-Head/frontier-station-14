using Content.Shared.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Player; // Frontier - Dont edit AI factions

namespace Content.Shared.Clothing.EntitySystems;

/// <summary>
/// Handles <see cref="FactionClothingComponent"/> faction adding and removal.
/// </summary>
public sealed class FactionClothingSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionClothingComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<FactionClothingComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<FactionClothingComponent> ent, ref GotEquippedEvent args)
    {
        if (!HasComp<ActorComponent>(args.Equipee)) // Frontier - Dont edit AI factions
            return; // Frontier - Dont edit AI factions

        TryComp<NpcFactionMemberComponent>(args.Equipee, out var factionComp);
        var faction = (args.Equipee, factionComp);
        ent.Comp.AlreadyMember = _faction.IsMember(faction, ent.Comp.Faction);

        _faction.AddFaction(faction, ent.Comp.Faction);
    }

    private void OnUnequipped(Entity<FactionClothingComponent> ent, ref GotUnequippedEvent args)
    {
        if (!HasComp<ActorComponent>(args.Equipee)) // Frontier - Dont edit AI factions
            return; // Frontier - Dont edit AI factions

        if (ent.Comp.AlreadyMember)
        {
            ent.Comp.AlreadyMember = false;
            return;
        }

        _faction.RemoveFaction(args.Equipee, ent.Comp.Faction);
    }
}
