using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;

namespace AncientChaos;

internal static class AncientChaosRuntime
{
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
}
