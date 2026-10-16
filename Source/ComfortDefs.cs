using RimWorld;
using rjw;
using Verse;

namespace ComfortDuty;

[StaticConstructorOnStartup]
internal static class ComfortStartup
{
    static ComfortStartup()
    {
        ComfortDefs.Initialize();
        ResourceAdapter.Initialize();
        Log.Message("[RJW Comfort] 1.1.0: native RJW jobs; job-bound sessions; lazy map caches; " + ResourceAdapter.Integration);
    }
}

internal static class ComfortDefs
{
    internal static HediffDef Gathered;
    internal static ThingDef FallbackResource;
    internal static JobDef RelieveJob, ExtractJob, QuickJob;
    internal static PreceptDef Cheating, Encouraged;
    internal static ThoughtDef Provided, ProvidedEncouraged, Received, ReceivedEncouraged;
    internal static HistoryEventDef NonSpouseEvent;
    internal static void Initialize()
    {
        Gathered = DefDatabase<HediffDef>.GetNamed("Comfort_GatheredCum");
        FallbackResource = DefDatabase<ThingDef>.GetNamed("Comfort_GatheredCum");
        RelieveJob = DefDatabase<JobDef>.GetNamed("ComfortRelieve");
        ExtractJob = DefDatabase<JobDef>.GetNamed("ComfortExtract");
        QuickJob = xxx.quick_sex;
        Cheating = DefDatabase<PreceptDef>.GetNamedSilentFail("Comfort_Cheating");
        Encouraged = DefDatabase<PreceptDef>.GetNamedSilentFail("Comfort_Encouraged");
        Provided = DefDatabase<ThoughtDef>.GetNamed("Comfort_Provided");
        ProvidedEncouraged = DefDatabase<ThoughtDef>.GetNamed("Comfort_ProvidedEncouraged");
        Received = DefDatabase<ThoughtDef>.GetNamed("Comfort_Received");
        ReceivedEncouraged = DefDatabase<ThoughtDef>.GetNamed("Comfort_ReceivedEncouraged");
        NonSpouseEvent = DefDatabase<HistoryEventDef>.GetNamedSilentFail("GotLovin_NonSpouse");
    }
}
