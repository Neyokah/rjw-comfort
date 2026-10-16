using System.Collections;
using HarmonyLib;
using rjw;
using Verse;

namespace ComfortDuty;

internal static class ResourceAdapter
{
    internal static ThingDef Output;
    internal static float FluidPerItem = 25f;
    internal static string Integration = "standalone resource";
    static SexFluidDef sourceFluid;
    static bool measurementWarning;

    internal static void Initialize()
    {
        FluidPerItem = 25f;
        Integration = "standalone resource";
        sourceFluid = DefDatabase<SexFluidDef>.GetNamedSilentFail("Cum");
        Output = DefDatabase<ThingDef>.GetNamedSilentFail("Cumpilation_Cum") ?? ComfortDefs.FallbackResource;
        if (sourceFluid == null) return;
        var type = AccessTools.TypeByName("Cumpilation.Gathering.FluidGatheringDef");
        if (type == null) return;
        try
        {
            var fluid = type.GetField("fluidDef");
            var thing = type.GetField("thingDef");
            var amount = type.GetField("fluidRequiredForOneUnit");
            if (fluid == null || thing == null || amount == null) throw new InvalidOperationException("unsupported gathering definition schema");
            var database = typeof(DefDatabase<>).MakeGenericType(type);
            var defs = (IEnumerable)database.GetProperty("AllDefsListForReading").GetValue(null);
            foreach (object def in defs)
                if (ReferenceEquals(fluid.GetValue(def), sourceFluid) && thing.GetValue(def) is ThingDef output &&
                    amount.GetValue(def) is float ratio && float.IsFinite(ratio) && ratio > 0f)
                { Output = output; FluidPerItem = ratio; Integration = "Cumpilation resource/conversion definitions"; return; }
        }
        catch (Exception e) { Log.Warning("[RJW Comfort] Optional gathering integration unavailable: " + e.Message); }
    }

    internal static float MeasureUnits(Pawn source)
    {
        if (source == null) return 0f;
        if (sourceFluid == null) return ComfortUtility.FallbackUnits;
        try
        {
            var parts = source.GetGenitalsList();
            if (parts == null) return ComfortUtility.FallbackUnits;
            float amount = 0f;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] is not ISexPartHediff part || part.Def?.produceFluidOnOrgasm != true) continue;
                var comp = part.GetPartComp();
                if (comp?.Fluid != sourceFluid || sourceFluid == null) continue;
                float produced = comp.FluidAmount;
                if (float.IsFinite(produced) && produced > 0f) amount += produced;
            }
            // A valid zero is zero; fallback is reserved for an unavailable integration.
            return float.IsFinite(amount) ? amount / FluidPerItem : 0f;
        }
        catch (Exception e)
        {
            if (!measurementWarning) { measurementWarning = true; Log.Warning("[RJW Comfort] Resource measurement unavailable; using configured fallback: " + e.Message); }
            return ComfortUtility.FallbackUnits;
        }
    }
}
