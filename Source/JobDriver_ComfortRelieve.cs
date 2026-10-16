using rjw;
using Verse;
using Verse.AI;

namespace ComfortDuty;

public class JobDriver_ComfortRelieve : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOn(() => !ComfortUtility.ApproachValid(pawn, job.targetA.Pawn));
        int parentId = job.loadID;
        AddFinishAction(condition => {
            if (condition != JobCondition.Succeeded) ComfortSession.Instance?.CancelPending(pawn, parentId);
        });
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        var handoff = ToilMaker.MakeToil("ComfortHandoff");
        handoff.defaultCompleteMode = ToilCompleteMode.Instant;
        handoff.initAction = () => {
            var target = job.targetA.Pawn;
            if (!ComfortUtility.CanStart(pawn, target, false)) return;
            var session = ComfortSession.Instance;
            if (session == null) return;
            Job next = JobMaker.MakeJob(ComfortDefs.QuickJob, target);
            if (!session.TryRegister(pawn, target, next, job, Find.TickManager.TicksGame)) return;
            try { pawn.jobs.jobQueue.EnqueueFirst(next); }
            catch { session.CancelPending(pawn, job.loadID); throw; }
        };
        yield return handoff;
    }
}
