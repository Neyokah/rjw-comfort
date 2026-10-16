using Verse;

namespace ComfortDuty;

// Lazy cache only: no map ticker and no saved pawn references.
public sealed class ComfortMap : MapComponent
{
    const int RefreshInterval = 120;
    readonly List<Thing> candidates = new();
    readonly Dictionary<int, Pawn> decisions = new();
    readonly Dictionary<int, IntVec3> dropCells = new();
    int snapshotTick = -1, population = -1;
    internal int Rebuilds;
    public ComfortMap(Map map) : base(map) { }
    public IReadOnlyList<Thing> Candidates { get { EnsureSnapshot(); return candidates; } }
    void EnsureSnapshot()
    {
        int now = Find.TickManager.TicksGame;
        var pawns = map.mapPawns.AllPawnsSpawned;
        if (CachePolicy.Reusable(snapshotTick, now, RefreshInterval) && population == pawns.Count) return;
        snapshotTick = now; population = pawns.Count;
        candidates.Clear(); decisions.Clear(); dropCells.Clear(); Rebuilds++;
        for (int i = 0; i < pawns.Count; i++) if (ComfortUtility.TargetEligible(pawns[i])) candidates.Add(pawns[i]);
    }
    internal bool AnyoneNeedsRelief(Pawn worker)
    {
        EnsureSnapshot();
        if (decisions.TryGetValue(worker.thingIDNumber, out var found))
        {
            if (found == null) return false;
            if (ComfortUtility.PairEligible(worker, found) && ComfortSession.Instance?.IsBusy(found) != true) return true;
            decisions.Remove(worker.thingIDNumber);
        }
        for (int i = 0; i < candidates.Count; i++)
            if (ComfortUtility.CanStart(worker, (Pawn)candidates[i], true))
            { decisions[worker.thingIDNumber] = (Pawn)candidates[i]; return true; }
        decisions[worker.thingIDNumber] = null; return false;
    }
    internal IntVec3 DropCell(Pawn worker)
    {
        EnsureSnapshot();
        if (!dropCells.TryGetValue(worker.thingIDNumber, out var cell))
        { cell = ComfortUtility.ComputeDropCell(worker); dropCells[worker.thingIDNumber] = cell; }
        return cell;
    }
}
internal static class CachePolicy
{
    internal static bool Reusable(int previous, int now, int interval) => previous >= 0 && now >= previous && now - previous < interval;
}
