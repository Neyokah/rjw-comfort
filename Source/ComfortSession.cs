using rjw;
using Verse;
using Verse.AI;

namespace ComfortDuty;

// Preserve the saved class/keys; new markers additionally bind to a real job.
public sealed class ComfortPair : IExposable
{
    public int workerId, partnerId, tick;
    public Pawn worker, partner;
    public int jobId = -1, parentJobId = -1;
    internal bool processing;
    internal JobDriver_SexQuick attachedDriver;
    internal Action<JobCondition> finish;
    internal Func<JobCondition> eligibility;
    public bool Matches(Pawn a, Pawn b) => a != null && b != null &&
        ((worker == a && partner == b) || (worker == b && partner == a));
    public void ExposeData()
    {
        Scribe_Values.Look(ref workerId, "workerId");
        Scribe_Values.Look(ref partnerId, "partnerId");
        Scribe_Values.Look(ref tick, "tick");
        Scribe_Values.Look(ref jobId, "jobId", -1);
        Scribe_Values.Look(ref parentJobId, "parentJobId", -1);
        Scribe_References.Look(ref worker, "worker");
        Scribe_References.Look(ref partner, "partner");
    }
}

public sealed class ComfortSession : GameComponent
{
    const int SweepInterval = 250, QueueTimeout = 250;
    List<ComfortPair> pairs = new();
    readonly Dictionary<int, ComfortPair> byPawn = new();
    static Game cachedGame;
    static ComfortSession cachedInstance;
    public ComfortSession(Game game) { }
    public static ComfortSession Instance
    {
        get
        {
            var game = Current.Game;
            if (!ReferenceEquals(game, cachedGame))
            { cachedGame = game; cachedInstance = game?.GetComponent<ComfortSession>(); }
            return cachedInstance;
        }
    }
    internal int Count => pairs.Count;
    public bool TryRegister(Pawn worker, Pawn partner, Job next, Job parent, int now)
    {
        if (worker == null || partner == null || worker == partner || next == null ||
            next.def != ComfortDefs.QuickJob || next.targetA.Pawn != partner || IsBusy(worker) || IsBusy(partner)) return false;
        var pair = new ComfortPair {
            worker = worker, partner = partner, workerId = worker.thingIDNumber,
            partnerId = partner.thingIDNumber, jobId = next.loadID,
            parentJobId = parent?.loadID ?? -1, tick = now
        };
        pairs.Add(pair); byPawn[pair.workerId] = pair; byPawn[pair.partnerId] = pair;
        return true;
    }
    internal ComfortPair FindForJob(Pawn worker, Job job)
    {
        if (worker == null || job == null || !byPawn.TryGetValue(worker.thingIDNumber, out var pair)) return null;
        return pair.worker == worker && pair.jobId == job.loadID && job.def == ComfortDefs.QuickJob &&
               job.targetA.Pawn == pair.partner ? pair : null;
    }
    public bool IsBusy(Pawn pawn)
    {
        if (pawn == null || !byPawn.TryGetValue(pawn.thingIDNumber, out var pair)) return false;
        if (IsLive(pair, Find.TickManager.TicksGame)) return true;
        Cancel(pair); return false;
    }
    internal bool IsLive(ComfortPair pair, int now)
    {
        var worker = pair.worker; var partner = pair.partner;
        if (worker == null || partner == null || worker.Dead || partner.Dead || !worker.Spawned ||
            !partner.Spawned || worker.Map != partner.Map) return false;
        if (FindForJob(worker, worker.CurJob) == pair) return true;
        if (now < pair.tick || now - pair.tick > QueueTimeout) return false;
        var queue = worker.jobs?.jobQueue;
        if (queue != null)
            for (int i = 0; i < queue.Count; i++)
                if (FindForJob(worker, queue[i].job) == pair) return true;
        return false;
    }
    internal void Attach(JobDriver_SexQuick driver)
    {
        var pair = FindForJob(driver.pawn, driver.job);
        if (pair == null) return;
        if (!ReferenceEquals(pair.attachedDriver, driver))
        {
            pair.attachedDriver = driver;
            pair.finish = _ => Cancel(pair);
            pair.eligibility = () => ComfortUtility.IsAdultPawn(pair.worker) && ComfortUtility.IsAdultPawn(pair.partner)
                ? JobCondition.Ongoing : JobCondition.Incompletable;
        }
        if (!driver.globalFinishActions.Contains(pair.finish)) driver.AddFinishAction(pair.finish);
        if (!driver.globalFailConditions.Contains(pair.eligibility)) driver.AddEndCondition(pair.eligibility);
    }
    internal ComfortPair BeginCompletion(SexProps props)
    {
        if (props?.pawn == null || props.partner == null || props.isRape ||
            !byPawn.TryGetValue(props.pawn.thingIDNumber, out var pair) || pair.processing || !pair.Matches(props.pawn, props.partner)) return null;
        if (FindForJob(pair.worker, pair.worker.CurJob) != pair ||
            pair.worker.jobs?.curDriver is not JobDriver_SexQuick driver || !ReferenceEquals(driver.Sexprops, props) ||
            !ComfortUtility.IsAdultPawn(pair.worker) || !ComfortUtility.IsAdultPawn(pair.partner)) return null;
        pair.processing = true; return pair;
    }
    internal bool Consume(ComfortPair pair)
    {
        if (pair == null || !byPawn.TryGetValue(pair.workerId, out var current) || current != pair) return false;
        Cancel(pair); return true;
    }
    internal void Cancel(ComfortPair pair)
    {
        if (pair == null) return;
        if (byPawn.TryGetValue(pair.workerId, out var workerPair) && workerPair == pair) byPawn.Remove(pair.workerId);
        if (byPawn.TryGetValue(pair.partnerId, out var targetPair) && targetPair == pair) byPawn.Remove(pair.partnerId);
        pairs.Remove(pair); pair.processing = false;
    }
    internal void CancelPending(Pawn worker, int parentId)
    {
        if (worker != null && byPawn.TryGetValue(worker.thingIDNumber, out var pair) &&
            pair.worker == worker && pair.parentJobId == parentId) Cancel(pair);
    }
    public override void GameComponentTick()
    {
        if (pairs.Count == 0) return;
        int now = Find.TickManager.TicksGame;
        if (now % SweepInterval != 0) return;
        for (int i = pairs.Count - 1; i >= 0; i--) if (!IsLive(pairs[i], now)) Cancel(pairs[i]);
    }
    public override void LoadedGame()
    {
        // Pawn drivers and game components can be post-loaded in different orders.
        for (int i = 0; i < pairs.Count; i++)
            if (pairs[i].worker?.jobs?.curDriver is JobDriver_SexQuick driver) Attach(driver);
    }
    public override void ExposeData()
    {
        base.ExposeData(); Scribe_Collections.Look(ref pairs, "pairs", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) RebuildAfterLoad();
    }
    internal void RebuildAfterLoad()
    {
        pairs ??= new List<ComfortPair>(); byPawn.Clear();
        // Legacy unbound markers are discarded; jobs and stored resources are untouched.
        for (int i = pairs.Count - 1; i >= 0; i--)
        {
            var pair = pairs[i];
            if (pair?.worker != null) pair.workerId = pair.worker.thingIDNumber;
            if (pair?.partner != null) pair.partnerId = pair.partner.thingIDNumber;
            if (pair?.worker == null || pair.partner == null || pair.worker == pair.partner || pair.jobId < 0 ||
                byPawn.ContainsKey(pair.workerId) || byPawn.ContainsKey(pair.partnerId))
            { pairs.RemoveAt(i); continue; }
            pair.processing = false; byPawn[pair.workerId] = pair; byPawn[pair.partnerId] = pair;
        }
    }
}
