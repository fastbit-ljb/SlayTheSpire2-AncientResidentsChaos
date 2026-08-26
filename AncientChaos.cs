using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;

namespace AncientChaos;

internal sealed class AncientChaosRunState
{
    public required Dictionary<string, RelicModel> RelicMapping { get; init; }
}

internal static class AncientChaosRuntime
{
    private static readonly ConditionalWeakTable<IRunState, AncientChaosRunState> States = new();
    private static readonly FieldInfo? RoomsField = AccessTools.Field(typeof(ActModel), "_rooms");

    public static AncientEventModel PickRandomAncient(Rng rng)
    {
        List<AncientEventModel> ancients = ModelDb.AllAncients.ToList();
        return rng.NextItem(ancients) ?? ModelDb.AllAncients.First();
    }

    public static void ReplaceActAncient(ActModel act, AncientEventModel ancient)
    {
        if (RoomsField?.GetValue(act) is RoomSet roomSet)
        {
            roomSet.Ancient = ancient;
        }
    }

    public static RelicModel MapAncientRelic(RelicModel relic, IRunState? runState)
    {
        if (runState == null || relic.Rarity != RelicRarity.Ancient)
        {
            return relic;
        }

        AncientChaosRunState state = States.GetValue(runState, BuildState);
        return state.RelicMapping.TryGetValue(relic.Id.Entry, out RelicModel? mapped) ? mapped.ToMutable() : relic;
    }

    private static AncientChaosRunState BuildState(IRunState runState)
    {
        List<RelicModel> ancientRelics = ModelDb.AllRelics
            .Where(relic => relic.Rarity == RelicRarity.Ancient)
            .ToList();

        if (ancientRelics.Count == 0)
        {
            return new AncientChaosRunState { RelicMapping = new Dictionary<string, RelicModel>() };
        }

        Rng rng = new((ulong)runState.Rng.Seed ^ 0xAC1DC0A5UL);
        List<RelicModel> shuffled = ancientRelics.UnstableShuffle(rng).ToList();
        Dictionary<string, RelicModel> mapping = new(ancientRelics.Count);
        for (int i = 0; i < ancientRelics.Count; i++)
        {
            mapping[ancientRelics[i].Id.Entry] = shuffled[i];
        }

        return new AncientChaosRunState { RelicMapping = mapping };
    }
}

[HarmonyPatch]
internal static class AncientChaosPatches
{
    [HarmonyPatch(typeof(ActModel), nameof(ActModel.GenerateRooms))]
    [HarmonyPostfix]
    private static void RandomizeAncientEncounter(ActModel __instance, Rng rng)
    {
        AncientChaosRuntime.ReplaceActAncient(__instance, AncientChaosRuntime.PickRandomAncient(rng));
    }

    [HarmonyPatch]
    private static class ShuffleAncientRelicOption
    {
        private static MethodBase? TargetMethod() => AccessTools.Method(
            typeof(EventModel),
            "RelicOption",
            new[] { typeof(RelicModel), typeof(Func<Task>), typeof(string) });

        [HarmonyPrefix]
        private static void Prefix(ref RelicModel relic, EventModel __instance)
        {
            if (__instance is AncientEventModel ancientEvent)
            {
                relic = AncientChaosRuntime.MapAncientRelic(relic, ancientEvent.Owner?.RunState);
            }
        }
    }
}
