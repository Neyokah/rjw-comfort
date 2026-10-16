using RimWorld;
using Verse;
using Verse.AI;

namespace ComfortDuty;

public class WorkGiver_ComfortExtract : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.OnCell;
    public override bool ShouldSkip(Pawn pawn, bool forced = false) => !ComfortUtility.ReadyToExtract(pawn);
    public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
    {
        if (ComfortUtility.ReadyToExtract(pawn)) yield return ComfortUtility.DropCell(pawn);
    }
    public override bool HasJobOnCell(Pawn pawn, IntVec3 cell, bool forced = false) =>
        ComfortUtility.HasExtractableResource(pawn) && cell == ComfortUtility.DropCell(pawn) &&
        !cell.IsForbidden(pawn) && pawn.CanReserveAndReach(cell, PathEndMode.OnCell, Danger.Some);
    public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false) => JobMaker.MakeJob(ComfortDefs.ExtractJob, cell);
}
