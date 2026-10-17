using HarmonyLib;
using RimWorld;
using rjw;
using Verse;

namespace ComfortDuty;

internal static class SubmissionToggle
{
    internal const int DurationTicks = GenDate.TicksPerDay;

    internal static bool IsActive(Pawn pawn) =>
        pawn?.health?.hediffSet?.HasHediff(RJWHediffDefOf.Submitting) == true;

    internal static void Toggle(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null) return;
        // Cancellation remains possible even if the pawn's eligibility changed.
        if (IsActive(pawn))
        {
            foreach (var hediff in pawn.health.hediffSet.hediffs
                         .Where(h => h.def == RJWHediffDefOf.Submitting).ToArray())
                pawn.health.RemoveHediff(hediff);
            return;
        }
        if (pawn.Dead || !ComfortUtility.IsAdultPawn(pawn)) return;
        var submission = HediffMaker.MakeHediff(RJWHediffDefOf.Submitting, pawn);
        var timer = submission.TryGetComp<HediffComp_Disappears>();
        if (timer == null)
        {
            Log.Warning("[RJW Comfort] Submit requires RJW's disappearing submission hediff; no state was applied.");
            return;
        }
        timer.SetDuration(DurationTicks);
        pawn.health.AddHediff(submission);
    }

    internal static void Configure(Command_Action command, Pawn pawn)
    {
        bool active = IsActive(pawn);
        command.defaultLabel = (active ? "Comfort_EndSubmission" : "Comfort_SubmitDay").Translate();
        command.defaultDesc = (active ? "Comfort_EndSubmissionDesc" : "Comfort_SubmitDayDesc").Translate();
        if (!active && !ComfortUtility.IsAdultPawn(pawn))
            command.Disable("Comfort_SubmitAdultOnly".Translate());
    }
}

// Keep RJW's action and its existing multiplayer sync entry point.
[HarmonyPatch(typeof(SubmitGizmo), "LayDownAndAccept")]
internal static class Patch_SubmitToggle
{
    internal static bool Prefix(Pawn pawn)
    {
        SubmissionToggle.Toggle(pawn);
        return false;
    }
}

[HarmonyPatch(typeof(SubmitGizmo), MethodType.Constructor, new[] { typeof(Pawn) })]
internal static class Patch_SubmitLabel
{
    internal static void Postfix(SubmitGizmo __instance, Pawn pawn) =>
        SubmissionToggle.Configure(__instance, pawn);
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
internal static class Patch_SubmissionCancellation
{
    internal static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result) =>
        __result = WithCancellation(__instance, __result);

    internal static IEnumerable<Gizmo> WithCancellation(Pawn pawn, IEnumerable<Gizmo> original)
    {
        bool hasSubmit = false;
        foreach (var gizmo in original)
        {
            if (gizmo is SubmitGizmo) hasSubmit = true;
            yield return gizmo;
        }
        // RJW normally hides Submit when undrafted. Keep the same native action
        // available to cancel a player's active submission in that state.
        if (!hasSubmit && SubmissionToggle.IsActive(pawn) && !pawn.Dead && pawn.IsColonist)
            yield return new SubmitGizmo(pawn) { Order = 6f };
    }
}
