using Verse;
using Verse.AI;

namespace ComfortDuty;

public class JobDriver_ComfortExtract : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA.Cell, job, 1, -1, null, errorOnFailed);
    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => !ComfortUtility.HasExtractableResource(pawn));
        yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
        var drop = ToilMaker.MakeToil("ComfortExtract");
        drop.defaultCompleteMode = ToilCompleteMode.Instant;
        drop.initAction = () => ComfortUtility.ExtractAt(pawn, job.targetA.Cell);
        yield return drop;
    }
}
