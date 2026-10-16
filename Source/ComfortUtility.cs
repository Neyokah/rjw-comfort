using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace ComfortDuty;

public enum ComfortStance { Accepted, Encouraged, Cheating }

public static class ComfortUtility
{
    const float JoyAmount = 0.4f;
    public static float FallbackUnits => ComfortMod.Settings?.fallbackUnits ?? 1f;
    public static float UnitsBeforeExtraction => ComfortMod.Settings?.unitsBeforeExtraction ?? 1f;
    public static HediffDef GatheredHediffDef => ComfortDefs.Gathered;
    public static ThingDef CumThingDef() => ResourceAdapter.Output;

    public static bool IsAdultPawn(Pawn pawn)
    {
        if (pawn?.ageTracker == null || !xxx.is_human(pawn) || xxx.is_mechanoid(pawn) ||
            pawn.ageTracker.AgeBiologicalYears < 18) return false;
        var stage = pawn.ageTracker.CurLifeStage;
        if (stage == null) return false;
        var dev = stage.developmentalStage;
        return AdultStagePolicy.Allowed(pawn.ageTracker.AgeBiologicalYears, pawn.GetAgeCategory(), dev);
    }

    public static bool CanComfort(Pawn pawn)
    {
        if (!IsAdultPawn(pawn) || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Drafted ||
            pawn.InMentalState || pawn.gender == Gender.None) return false;
        var orientation = pawn.GetCompRJW()?.orientation ?? Orientation.None;
        return orientation != Orientation.None && orientation != Orientation.Asexual;
    }

    public static bool IsColonyPawn(Pawn pawn) => pawn != null &&
        (pawn.IsColonist || pawn.IsSlaveOfColony || pawn.IsPrisonerOfColony);

    public static bool TargetEligible(Pawn pawn)
    {
        if (!IsColonyPawn(pawn) || !CanComfort(pawn) || pawn.health?.capacities?.CanBeAwake != true ||
            pawn.jobs?.curDriver is JobDriver_Sex) return false;
        var need = pawn.needs?.TryGetNeed<Need_Sex>();
        return need != null && need.CurLevel <= need.thresh_horny();
    }

    // RJW owns orientation/anatomy handling; keep selection deterministic and prefer the
    // strong preference, matching Comfort's existing orientation policy without rolling RNG per tick.
    public static bool AcceptsGender(Pawn observer, Pawn other) => observer != null && other != null &&
        observer.gender != Gender.None && other.gender != Gender.None && OrientationUtility.PreferenceFactor(observer, other) >= 1f;

    internal static bool ApproachValid(Pawn worker, Pawn target) =>
        worker != target && CanComfort(worker) && TargetEligible(target) && worker.Map == target.Map &&
        !target.IsForbidden(worker) && !worker.HostileTo(target) && !target.HostileTo(worker);
    public static bool PairEligible(Pawn worker, Pawn target) => ApproachValid(worker, target) &&
        AcceptsGender(worker, target) && AcceptsGender(target, worker);

    public static bool CanStart(Pawn worker, Pawn target, bool checkReservation)
    {
        if (!PairEligible(worker, target) || ComfortSession.Instance?.IsBusy(worker) == true ||
            ComfortSession.Instance?.IsBusy(target) == true) return false;
        var targetJob = target.CurJob;
        if (targetJob != null && (targetJob.playerForced || !CasualSex_Helper.quickieAllowedJobs.Contains(targetJob.def.defName))) return false;
        if (!CasualSex_Helper.CanHaveSex(worker) || !CasualSex_Helper.CanHaveSex(target) ||
            !CasualSex_Helper.CanTargetHookup(worker, target) || !CasualSex_Helper.CanTargetHookup(target, worker)) return false;
        if (!worker.CanReach(target, PathEndMode.Touch, Danger.Some)) return false;
        return !checkReservation || (worker.CanReserve(target, 1, -1, null, false) && target.CanReserve(worker, 1, -1, null, false));
    }

    public static bool StillValid(Pawn worker, Pawn target, bool checkReserve) => CanStart(worker, target, checkReserve);
    public static bool AnyoneNeedsRelief(Pawn worker) => worker?.Map?.GetComponent<ComfortMap>()?.AnyoneNeedsRelief(worker) == true;

    public static bool HasExtractableResource(Pawn pawn)
    {
        if (!IsAdultPawn(pawn) || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Drafted) return false;
        var stored = pawn.health?.hediffSet?.GetFirstHediffOfDef(ComfortDefs.Gathered);
        return stored != null && float.IsFinite(stored.Severity) && stored.Severity >= UnitsBeforeExtraction;
    }

    public static bool ReadyToExtract(Pawn pawn) => HasExtractableResource(pawn) &&
        ComfortSession.Instance?.IsBusy(pawn) != true && !AnyoneNeedsRelief(pawn);
    public static IntVec3 DropCell(Pawn pawn) => pawn.Map.GetComponent<ComfortMap>().DropCell(pawn);

    internal static IntVec3 ComputeDropCell(Pawn pawn)
    {
        var def = ResourceAdapter.Output;
        if (pawn.Map == null || def == null) return pawn.Position;
        var sample = ThingMaker.MakeThing(def);
        try
        {
            if (StoreUtility.TryFindBestBetterStoreCellFor(sample, pawn, pawn.Map, StoragePriority.Unstored,
                pawn.Faction, out var cell, true) && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Some)) return cell;
        }
        finally { if (!sample.Destroyed) sample.Destroy(DestroyMode.Vanish); }
        return pawn.Position;
    }

    public static void AddGathered(Pawn worker, float units)
    {
        if (worker?.health == null || !float.IsFinite(units) || units <= 0f) return;
        var def = ComfortDefs.Gathered;
        var stored = worker.health.hediffSet.GetFirstHediffOfDef(def);
        if (stored == null)
        {
            stored = HediffMaker.MakeHediff(def, worker);
            stored.Severity = Math.Min(def.maxSeverity, units);
            worker.health.AddHediff(stored);
        }
        else stored.Severity = Math.Min(def.maxSeverity, stored.Severity + units);
    }

    public static void ExtractAt(Pawn pawn, IntVec3 cell)
    {
        if (pawn?.Map == null) return;
        var stored = pawn.health?.hediffSet?.GetFirstHediffOfDef(ComfortDefs.Gathered);
        var def = ResourceAdapter.Output;
        if (stored == null || def == null || def.stackLimit < 1 || !float.IsFinite(stored.Severity)) return;
        while (stored.Severity >= 1f)
        {
            int requested = Math.Min((int)Math.Floor(stored.Severity), def.stackLimit);
            var item = ThingMaker.MakeThing(def);
            item.stackCount = requested;
            var receipt = new PlacementReceipt(requested, quantity => stored.Severity = TransferMath.Deduct(stored.Severity, quantity));
            try
            {
                // Account at the actual placement callback, including partial merges followed by failure.
                GenPlace.TryPlaceThing(item, cell, pawn.Map, ThingPlaceMode.Near, placedAction: receipt.Record);
            }
            finally
            {
                if (!item.Spawned && !item.Destroyed) item.Destroy(DestroyMode.Vanish);
            }
            if (receipt.Placed < requested) break;
        }
        if (stored.Severity <= 0.001f) pawn.health.RemoveHediff(stored);
    }

    public static ComfortStance Stance(Pawn pawn)
    {
        if (pawn?.Ideo == null || !ModsConfig.IdeologyActive) return ComfortStance.Accepted;
        if (ComfortDefs.Cheating != null && pawn.Ideo.HasPrecept(ComfortDefs.Cheating)) return ComfortStance.Cheating;
        return ComfortDefs.Encouraged != null && pawn.Ideo.HasPrecept(ComfortDefs.Encouraged) ? ComfortStance.Encouraged : ComfortStance.Accepted;
    }
    public static bool CountsAsCheating(Pawn pawn) => Stance(pawn) == ComfortStance.Cheating;

    public static void GiveReliefRewards(Pawn worker, Pawn partner)
    {
        GiveJoy(worker); GiveJoy(partner);
        GiveThought(worker, true); GiveThought(partner, false);
        ApplyCheating(worker, partner); ApplyCheating(partner, worker);
        AddGathered(worker, ResourceAdapter.MeasureUnits(partner));
    }
    static void GiveJoy(Pawn pawn) => pawn?.needs?.TryGetNeed<Need_Joy>()?.GainJoy(JoyAmount, JoyKindDefOf.Social);
    static void GiveThought(Pawn pawn, bool provider)
    {
        var memories = pawn?.needs?.mood?.thoughts?.memories;
        if (memories == null) return;
        var stance = Stance(pawn);
        if (stance == ComfortStance.Cheating) return;
        var thought = provider ? (stance == ComfortStance.Encouraged ? ComfortDefs.ProvidedEncouraged : ComfortDefs.Provided)
            : (stance == ComfortStance.Encouraged ? ComfortDefs.ReceivedEncouraged : ComfortDefs.Received);
        if (thought != null) memories.TryGainMemory(thought);
    }
    static void ApplyCheating(Pawn actor, Pawn partner)
    {
        if (!CountsAsCheating(actor) || actor.relations == null) return;
        var relations = actor.relations.DirectRelations;
        for (int i = 0; i < relations.Count; i++)
        {
            var relation = relations[i];
            if (relation.def != PawnRelationDefOf.Spouse || relation.otherPawn == null || relation.otherPawn == partner || relation.otherPawn.Dead) continue;
            if (ComfortMod.AddCheaterThought != null) ComfortMod.AddCheaterThought(relation.otherPawn, actor);
            else relation.otherPawn.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.CheatedOnMe, actor);
        }
        if (partner != null && actor.relations.DirectRelationExists(PawnRelationDefOf.Spouse, partner)) return;
        if (ComfortDefs.NonSpouseEvent != null && Find.HistoryEventsManager != null)
            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(ComfortDefs.NonSpouseEvent, actor.Named(HistoryEventArgsNames.Doer)), true);
    }
}

internal static class AdultStagePolicy
{
    internal static bool Allowed(int years, AgeCategory category, DevelopmentalStage stage) =>
        years >= 18 && category == AgeCategory.Adult && Allowed(stage);
    internal static bool Allowed(DevelopmentalStage stage) => (stage & DevelopmentalStage.Adult) != 0 &&
        (stage & (DevelopmentalStage.Newborn | DevelopmentalStage.Baby | DevelopmentalStage.Child)) == 0;
}
internal static class TransferMath
{
    internal static float Deduct(float balance, int placed) => placed > 0 ? Math.Max(0f, balance - placed) : balance;
}
internal sealed class PlacementReceipt
{
    readonly int requested;
    readonly Action<int> account;
    internal int Placed { get; private set; }
    internal PlacementReceipt(int requested, Action<int> account) { this.requested = requested; this.account = account; }
    internal void Record(Thing item, int quantity)
    {
        int accepted = Math.Min(Math.Max(quantity, 0), requested - Placed);
        if (accepted == 0) return;
        Placed += accepted;
        account(accepted);
    }
}
