using HarmonyLib;
using RimWorld;
using rjw;
using Verse;

namespace ComfortDuty;

internal sealed class CompletionScope
{
    [ThreadStatic] internal static CompletionScope Current;
    internal readonly ComfortSession Session;
    internal readonly ComfortPair Pair;
    internal readonly CompletionScope Previous;
    internal CompletionScope(ComfortSession session, ComfortPair pair)
    { Session = session; Pair = pair; Previous = Current; Current = this; }
    internal bool Matches(Pawn a, Pawn b) => Pair?.Matches(a, b) == true;
}

[HarmonyPatch(typeof(SexUtility), nameof(SexUtility.ProcessSex))]
public static class Patch_ProcessSex
{
    internal static void Prefix(SexProps props, out CompletionScope __state)
    {
        __state = null;
        var session = ComfortSession.Instance;
        var pair = session?.BeginCompletion(props);
        // A nested unrelated call must temporarily hide the outer Comfort context.
        if (pair != null || CompletionScope.Current != null) __state = new CompletionScope(session, pair);
    }
    internal static void Postfix(CompletionScope __state, bool __runOriginal)
    {
        if (!__runOriginal || __state?.Pair == null || !__state.Session.Consume(__state.Pair)) return;
        try { ComfortUtility.GiveReliefRewards(__state.Pair.worker, __state.Pair.partner); }
        catch (Exception e) { Log.Error("[RJW Comfort] Completion effects failed: " + e); }
    }
    internal static void Finalizer(CompletionScope __state)
    {
        if (__state == null) return;
        if (__state.Pair != null) __state.Session.Cancel(__state.Pair);
        CompletionScope.Current = __state.Previous;
    }
}

[HarmonyPatch(typeof(JobDriver_SexQuick), "MakeNewToils")]
public static class Patch_QuickJobLifecycle
{
    public static void Postfix(JobDriver_SexQuick __instance) => ComfortSession.Instance?.Attach(__instance);
}

[HarmonyPatch(typeof(AfterSexUtility), nameof(AfterSexUtility.think_about_sex_Consensual))]
public static class Patch_ThinkAboutSexConsensual
{
    public static bool Prefix(Pawn pawn, Pawn partner, ref ThoughtDef __result)
    {
        if (CompletionScope.Current?.Matches(pawn, partner) != true) return true;
        __result = null; return false;
    }
}

public static class Patch_TryAddCheaterThought
{
    public static bool Prefix(Pawn pawn, Pawn cheater)
    {
        var pair = CompletionScope.Current?.Pair;
        if (pair == null || (cheater != pair.worker && cheater != pair.partner)) return true;
        return ComfortUtility.CountsAsCheating(cheater);
    }
}
