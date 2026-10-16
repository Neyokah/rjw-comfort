using RimWorld;
using rjw;
using Verse;
using Verse.AI;

namespace ComfortDuty;

public class WorkGiver_ComfortRelieve : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.Touch;
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
    public override bool Prioritized => true;
    public override bool ShouldSkip(Pawn pawn, bool forced = false) => !ComfortUtility.CanComfort(pawn) || ComfortSession.Instance?.IsBusy(pawn) == true;
    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => pawn.Map?.GetComponent<ComfortMap>()?.Candidates;
    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => ComfortUtility.CanStart(pawn, t as Pawn, true);
    public override float GetPriority(Pawn pawn, TargetInfo t)
    {
        var need = (t.Thing as Pawn)?.needs?.TryGetNeed<Need_Sex>();
        return need == null ? 0f : 1f - need.CurLevel;
    }
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false) => JobMaker.MakeJob(ComfortDefs.RelieveJob, t);
}
