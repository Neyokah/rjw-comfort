using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using ComfortDuty;
using HarmonyLib;
using RimWorld;
using rjw;
using Verse;
using Verse.AI;

internal static class Program
{
    static int checks;
    static readonly string GameDir = Environment.GetEnvironmentVariable("RIMWORLD_MANAGED_DIR")!;
    static readonly string RjwDir = Environment.GetEnvironmentVariable("RJW_ASSEMBLIES")!;
    static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); checks++; }
    static T Empty<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    static void Patch(Harmony h, Type type, string method, string prefix) => h.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(Fakes), prefix));

    static int Main()
    {
        AssemblyLoadContext.Default.Resolving += (_, name) => {
            foreach (string dir in new[] { GameDir, RjwDir }) { string p = Path.Combine(dir, name.Name + ".dll"); if (File.Exists(p)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(p); }
            return null;
        };
        try { Run(); Console.WriteLine($"PASS {checks} checks under the installed RimWorld compatibility runtime."); return 0; }
        catch (Exception e) { Console.WriteLine(e); return 1; }
    }

    static void Run()
    {
        Console.WriteLine("Game: " + AssemblyName.GetAssemblyName(Path.Combine(GameDir, "Assembly-CSharp.dll")));
        Console.WriteLine("RJW: " + AssemblyName.GetAssemblyName(Path.Combine(RjwDir, "RJW.dll")));
        var h = new Harmony("comfort.tests.fixtures");
        h.Patch(AccessTools.PropertyGetter(typeof(Prefs), nameof(Prefs.LogVerbose)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.False)));
        h.Patch(AccessTools.PropertyGetter(typeof(ComfortSession), nameof(ComfortSession.Instance)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Session)));
        h.Patch(AccessTools.PropertyGetter(typeof(Find), nameof(Find.TickManager)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Clock)));
        h.Patch(AccessTools.PropertyGetter(typeof(TickManager), nameof(TickManager.TicksGame)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Ticks)));
        h.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Spawned)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Spawned)));
        h.Patch(AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Map)));
        h.Patch(AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.Dead)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Dead)));
        Patch(h, typeof(ComfortUtility), nameof(ComfortUtility.IsAdultPawn), nameof(Fakes.Adult));
        Patch(h, typeof(ComfortUtility), nameof(ComfortUtility.GiveReliefRewards), nameof(Fakes.Rewards));
        Patch(h, typeof(ComfortUtility), nameof(ComfortUtility.CountsAsCheating), nameof(Fakes.Cheating));
        Patch(h, typeof(ComfortUtility), nameof(ComfortUtility.TargetEligible), nameof(Fakes.Target));
        h.Patch(AccessTools.Method(typeof(SexUtility), nameof(SexUtility.ProcessSex)), transpiler: new HarmonyMethod(typeof(Fakes), nameof(Fakes.ProcessBody)));
        // These are the actual production attributes/target signatures, using the installed RJW binary.
        new Harmony("comfort.tests.production").PatchAll(typeof(ComfortSession).Assembly);
        Console.WriteLine("All production Harmony targets installed successfully.");
        ComfortDefs.QuickJob = new JobDef { defName = "Quickie" };
        Fakes.Manager = Empty<TickManager>();
        Fakes.Now = 10;

        for (int age = 0; age < 100; age++)
        foreach (var category in Enum.GetValues<AgeCategory>())
        foreach (var stage in Enum.GetValues<DevelopmentalStage>())
        {
            bool expected = age >= 18 && category == AgeCategory.Adult && (stage & DevelopmentalStage.Adult) != 0 &&
                (stage & (DevelopmentalStage.Newborn | DevelopmentalStage.Baby | DevelopmentalStage.Child)) == 0;
            Check(AdultStagePolicy.Allowed(age, category, stage) == expected, "adult policy boundary");
        }
        Check(!AdultStagePolicy.Allowed(18, AgeCategory.Adult, DevelopmentalStage.Adult | DevelopmentalStage.Child), "mixed stages excluded");
        Check(AdultStagePolicy.Allowed(18, AgeCategory.Adult, DevelopmentalStage.Adult), "adult stage needs no English def name");
        var settings = new ComfortSettings { fallbackUnits = float.NaN, unitsBeforeExtraction = float.PositiveInfinity };
        settings.Sanitize(); Check(settings.fallbackUnits == 1 && settings.unitsBeforeExtraction == 1, "invalid saved settings sanitized");

        // Fuzz the actual placement receipt: partial success, partial failure, callback repetition and fractions.
        var rnd = new Random(906);
        for (int i = 0; i < 10000; i++)
        {
            float balance = rnd.Next(1, 21) + 0.25f; float original = balance;
            int requested = (int)balance; var receipt = new PlacementReceipt(requested, n => balance = TransferMath.Deduct(balance, n));
            int actual = rnd.Next(requested + 1), first = rnd.Next(actual + 1);
            receipt.Record(null, first); receipt.Record(null, actual - first);
            Check(receipt.Placed == actual && balance + actual == original, "partial placement conserves exact balance");
            receipt.Record(null, -2); Check(balance + actual == original, "invalid negative placement ignored");
        }
        float stored = 7.75f;
        var failed = new PlacementReceipt(7, n => stored = TransferMath.Deduct(stored, n));
        try { failed.Record(null, 3); throw new InvalidOperationException("placement fixture"); }
        catch (InvalidOperationException) { }
        Check(stored == 4.75f && failed.Placed == 3, "placed quantity accounted before placement exception");

        NewSession();
        var a = MakePawn(1); var b = MakePawn(2); var c = MakePawn(3);
        var job = Job(a, b, 101);
        Check(Fakes.Current.TryRegister(a, b, job, null, Fakes.Now), "register exact queued job");
        a.jobs.jobQueue.EnqueueFirst(job);
        Check(Fakes.Current.IsBusy(a) && Fakes.Current.IsBusy(b), "queued job remains tracked");
        a.jobs.jobQueue.Extract(job);
        Check(!Fakes.Current.IsBusy(a) && Fakes.Current.Count == 0, "removed queued job clears stale marker immediately");
        job = Job(a, b, 102);
        Fakes.Current.TryRegister(a, b, job, null, Fakes.Now); a.jobs.jobQueue.EnqueueFirst(job); Fakes.Now += 251;
        Check(!Fakes.Current.IsBusy(b), "queued markers expire without a running job");
        a.jobs.jobQueue.Extract(job);

        NewSession(); var pairJob = Activate(a, b, 201);
        var pair = Fakes.Current.FindForJob(a, pairJob);
        Check(Fakes.Current.FindForJob(a, Job(a, b, 202)) == null, "same pair with different job is not Comfort");
        Check(Fakes.Current.FindForJob(a, Job(a, c, 201)) == null, "wrong partner with same ID is not Comfort");
        var driver = (JobDriver_SexQuick)a.jobs.curDriver;
        var makeToils = AccessTools.Method(typeof(JobDriver_SexQuick), "MakeNewToils");
        _ = makeToils.Invoke(driver, null); // Iterator body stays unevaluated; production lifecycle postfix attaches.
        Check(driver.globalFinishActions.Count == 1 && driver.globalFailConditions.Count == 1, "native job finish and eligibility callbacks attached");
        driver.Cleanup(JobCondition.InterruptForced);
        Check(Fakes.Current.Count == 0 && !Fakes.Current.IsBusy(b), "real job cleanup clears both participants");

        NewSession(); Activate(a, b, 301);
        var props = ((JobDriver_SexQuick)a.jobs.curDriver).Sexprops;
        Fakes.RewardCalls = 0; Fakes.Suppressed = 0;
        SexUtility.ProcessSex(props);
        Check(Fakes.RewardCalls == 1 && Fakes.Suppressed == 1 && Fakes.Current.Count == 0, "exact native completion awards once");
        Check(CompletionScope.Current == null, "completion context restored after success");
        SexUtility.ProcessSex(props);
        Check(Fakes.RewardCalls == 1 && Fakes.Suppressed == 1, "repeated callback is an ordinary event");
        Check(Patch_TryAddCheaterThought.Prefix(c, a), "unrelated relationship event untouched");

        NewSession(); Activate(a, b, 401);
        props = ((JobDriver_SexQuick)a.jobs.curDriver).Sexprops;
        Fakes.Throw = true;
        try { SexUtility.ProcessSex(props); throw new Exception("Expected fixture exception"); }
        catch (InvalidOperationException e) { Check(e.Message == "fixture failure", "original exception propagates"); }
        finally { Fakes.Throw = false; }
        Check(Fakes.Current.Count == 0 && CompletionScope.Current == null, "exception clears session and context");

        NewSession(); Activate(a, b, 501);
        props = ((JobDriver_SexQuick)a.jobs.curDriver).Sexprops;
        Fakes.Nested = new SexProps(a, b); Fakes.NestedWasSuppressed = false;
        Fakes.RewardCalls = 0; SexUtility.ProcessSex(props);
        Check(Fakes.RewardCalls == 1 && !Fakes.NestedWasSuppressed, "unrelated nested event cannot inherit Comfort effects");
        Check(CompletionScope.Current == null, "nested context restored");

        NewSession(); Activate(a, b, 601);
        props = ((JobDriver_SexQuick)a.jobs.curDriver).Sexprops; Fakes.RewardCalls = 0;
        h.Patch(AccessTools.Method(typeof(SexUtility), nameof(SexUtility.ProcessSex)), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Skip)) { priority = Priority.Last });
        SexUtility.ProcessSex(props);
        Check(Fakes.RewardCalls == 0 && Fakes.Current.Count == 0 && CompletionScope.Current == null, "another mod skipping original cannot grant false completion");
        h.Unpatch(AccessTools.Method(typeof(SexUtility), nameof(SexUtility.ProcessSex)), AccessTools.Method(typeof(Fakes), nameof(Fakes.Skip)));

        NewSession(); Activate(a, b, 701); pair = Fakes.Current.FindForJob(a, a.CurJob);
        var list = (List<ComfortPair>)AccessTools.Field(typeof(ComfortSession), "pairs").GetValue(Fakes.Current);
        list.Add(new ComfortPair { workerId = 42, partnerId = 43, tick = Fakes.Now });
        pair.workerId = -2; pair.partnerId = -3;
        Fakes.Current.RebuildAfterLoad();
        Check(Fakes.Current.Count == 1 && Fakes.Current.FindForJob(a, a.CurJob) == pair, "save migration drops unbound markers and rebuilds reference indexes");
        Fakes.Current.Cancel(pair); var newer = Activate(a, c, 702); Fakes.Current.Cancel(pair);
        Check(Fakes.Current.FindForJob(a, newer) != null, "late old cleanup cannot remove new session");

        TestSaveRoundTrip(a, c);
        TestOptionalResourceDefinitions(h);
        TestMapCache(h);
        TestLegacySearchComparison(h);
        TestCompiledContract();
    }

    static void NewSession() { Fakes.Current = new ComfortSession(null); }
    static Pawn MakePawn(int id)
    {
        var p = Empty<Pawn>(); p.thingIDNumber = id; p.def = Empty<ThingDef>(); p.def.defName = "Human";
        p.jobs = new Pawn_JobTracker(p); Fakes.Pawns.Add(p);
        return p;
    }
    static Job Job(Pawn worker, Pawn target, int id) => new Job { def = ComfortDefs.QuickJob, loadID = id, targetA = target };
    static Job Activate(Pawn worker, Pawn target, int id)
    {
        worker.jobs.curJob = null; worker.jobs.curDriver = null;
        var j = Job(worker, target, id);
        Check(Fakes.Current.TryRegister(worker, target, j, null, Fakes.Now), "activate fixture session");
        var d = new JobDriver_SexQuick { pawn = worker, job = j, Sexprops = new SexProps(worker, target) };
        worker.jobs.curJob = j; worker.jobs.curDriver = d; return j;
    }
    static void TestSaveRoundTrip(Pawn a, Pawn b)
    {
        NewSession(); Activate(a, b, 880);
        var original = Fakes.Current;
        string file = Path.Combine(Path.GetTempPath(), "Comfort-tests-session-" + Guid.NewGuid() + ".xml");
        Scribe.saver.InitSaving(file, "ComfortRewriteTest");
        Scribe_Deep.Look(ref original, "session", new object[] { null });
        Scribe.saver.FinalizeSaving();
        ComfortSession restored = null;
        Scribe.loader.InitLoading(file);
        ((LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs)).RegisterLoaded(a);
        ((LoadedObjectDirectory)AccessTools.Field(typeof(CrossRefHandler), "loadedObjectDirectory").GetValue(Scribe.loader.crossRefs)).RegisterLoaded(b);
        Scribe_Deep.Look(ref restored, "session", new object[] { null });
        Scribe.loader.FinalizeLoading();
        Check(restored != null && restored.Count == 1 && restored.FindForJob(a, a.CurJob) != null, "actual Scribe save/load keeps exact job and pawn references");
        Fakes.Current = restored;
        restored.LoadedGame(); restored.LoadedGame();
        var resumedDriver = (JobDriver_SexQuick)a.jobs.curDriver;
        Check(resumedDriver.globalFinishActions.Count == 1, "post-load lifecycle binding is idempotent");
        resumedDriver.Cleanup(JobCondition.InterruptForced);
        Check(restored.Count == 0, "resumed job cancels loaded session correctly");
        var xml = System.Xml.Linq.XDocument.Load(file);
        foreach (var e in xml.Descendants().Where(e => new[] { "worker", "partner", "jobId", "parentJobId" }.Contains(e.Name.LocalName)).ToList()) e.Remove();
        string legacy = file + ".legacy.xml"; xml.Save(legacy);
        ComfortSession older = null;
        Scribe.loader.InitLoading(legacy);
        Scribe_Deep.Look(ref older, "session", new object[] { null });
        Scribe.loader.FinalizeLoading();
        Check(older != null && older.Count == 0, "actual Scribe migration safely drops legacy unbound sessions");
        Console.WriteLine("Actual Scribe session round-trip and legacy-format migration passed.");
    }
    static void TestOptionalResourceDefinitions(Harmony h)
    {
        var fallback = Empty<ThingDef>(); fallback.defName = "Comfort_GatheredCum";
        ComfortDefs.FallbackResource = fallback;
        DefDatabase<ThingDef>.Add(fallback);
        var fluid = new SexFluidDef { defName = "Cum" }; DefDatabase<SexFluidDef>.Add(fluid);
        ResourceAdapter.Initialize(); Check(ResourceAdapter.Output == fallback && ResourceAdapter.FluidPerItem == 25, "standalone resource fallback");
        var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Environment.GetEnvironmentVariable("CUMPILATION_DLL")!);
        var type = asm.GetType("Cumpilation.Gathering.FluidGatheringDef", true);
        var external = (Def)Activator.CreateInstance(type); external.defName = "FixtureExternalMapping";
        var item = Empty<ThingDef>(); item.defName = "FixtureExternalResource";
        type.GetField("fluidDef").SetValue(external, fluid); type.GetField("thingDef").SetValue(external, item);
        type.GetField("fluidRequiredForOneUnit").SetValue(external, 40f);
        var defs = (IList)typeof(DefDatabase<>).MakeGenericType(type).GetProperty("AllDefsListForReading").GetValue(null); defs.Add(external);
        ResourceAdapter.Initialize();
        Check(ResourceAdapter.Output == item && ResourceAdapter.FluidPerItem == 40, "uses actual installed optional mapping schema and conversion data");
        Check(ResourceAdapter.MeasureUnits(null) == 0, "missing source cannot create resources");
        h.Patch(AccessTools.Method(typeof(rjw.PawnExtensions), "GetGenitalsList"), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.Parts)));
        ComfortMod.Settings = new ComfortSettings { fallbackUnits = 0.75f };
        var source = MakePawn(999);
        Fakes.PartList = new List<Hediff>();
        Check(ResourceAdapter.MeasureUnits(source) == 0, "valid zero production never creates fallback resources");
        Fakes.PartList = null;
        Check(ResourceAdapter.MeasureUnits(source) == 0.75f, "unavailable integration uses configured fallback");
        Console.WriteLine("Optional Cumpilation definition integration and zero/fallback distinction passed.");
    }
    static void TestMapCache(Harmony h)
    {
        var map = Empty<Map>(); var mapPawns = Empty<MapPawns>(); map.mapPawns = mapPawns;
        var field = AccessTools.Field(typeof(MapPawns), "pawnsSpawned");
        if (field == null) throw new Exception("MapPawns fixture field missing: " + string.Join(",", typeof(MapPawns).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => f.Name)));
        var pawns = Enumerable.Range(1000, 500).Select(MakePawn).ToList(); field.SetValue(mapPawns, pawns);
        var cache = new ComfortMap(map); Fakes.TargetChecks = 0;
        int total = 0; var watch = Stopwatch.StartNew();
        for (int tick = 1000; tick < 1600; tick++)
        {
            Fakes.Now = tick;
            for (int worker = 0; worker < 100; worker++) total += cache.Candidates.Count;
        }
        watch.Stop();
        Check(cache.Rebuilds == 5 && Fakes.TargetChecks == 2500, "actual lazy map component shares five refreshes across 60,000 queries");
        Fakes.Now = 1600; pawns.Add(MakePawn(1999)); _ = cache.Candidates;
        Check(cache.Rebuilds == 6, "population change refreshes cache");
        Fakes.Now = 2; _ = cache.Candidates; Check(cache.Rebuilds == 7, "clock rewind invalidates cache");
        Console.WriteLine($"Candidate cache workload: 100 workers x 600 ticks, 500 pawns: 60,000 reads, 5 population passes / 2,500 filters, {watch.Elapsed.TotalMilliseconds:F2} ms. Synthetic lookup workload, not a TPS claim. Checksum {total}.");
    }
    static void TestLegacySearchComparison(Harmony h)
    {
        if (!File.Exists(Environment.GetEnvironmentVariable("COMFORT_LEGACY_DLL"))) { Console.WriteLine("SKIP legacy benchmark: set COMFORT_LEGACY_DLL to an original 1.0 assembly."); return; }
        string legacyPath = Path.Combine(Path.GetTempPath(), "Comfort-benchmark-" + Guid.NewGuid() + ".dll");
        using (var m = Mono.Cecil.ModuleDefinition.ReadModule(Environment.GetEnvironmentVariable("COMFORT_LEGACY_DLL")!))
        { m.Assembly.Name.Name = "Comfort.LegacyBenchmark"; m.Name = "Comfort.LegacyBenchmark.dll"; m.Write(legacyPath); }
        var legacy = AssemblyLoadContext.Default.LoadFromAssemblyPath(legacyPath);
        var utility = legacy.GetType("ComfortDuty.ComfortUtility", true);
        h.Patch(AccessTools.Method(utility, "StillValid"), prefix: new HarmonyMethod(typeof(Fakes), nameof(Fakes.LegacyInvalid)));
        var baseline = (Func<Pawn, bool>)AccessTools.Method(utility, "AnyoneNeedsRelief").CreateDelegate(typeof(Func<Pawn, bool>));
        var map = Empty<Map>(); var mapPawns = Empty<MapPawns>(); map.mapPawns = mapPawns;
        var pawns = Enumerable.Range(4000, 500).Select(MakePawn).ToList();
        AccessTools.Field(typeof(MapPawns), "pawnsSpawned").SetValue(mapPawns, pawns);
        Fakes.FixtureMap = map; Fakes.NoEligible = true;
        var workers = Enumerable.Range(5000, 100).Select(MakePawn).ToList();
        Fakes.LegacyChecks = 0; var watch = Stopwatch.StartNew();
        for (int tick = 2000; tick < 2600; tick++)
        { Fakes.Now = tick; foreach (var worker in workers) if (baseline(worker)) throw new Exception("legacy fixture should have no targets"); }
        watch.Stop(); double baselineMs = watch.Elapsed.TotalMilliseconds;
        var cache = new ComfortMap(map); Fakes.TargetChecks = 0; watch.Restart();
        for (int tick = 2000; tick < 2600; tick++)
        { Fakes.Now = tick; foreach (var worker in workers) if (cache.AnyoneNeedsRelief(worker)) throw new Exception("rewrite fixture should have no targets"); }
        watch.Stop(); Fakes.NoEligible = false;
        Check(Fakes.LegacyChecks == 30000000 && Fakes.TargetChecks == 2500, "same no-target workload: actual old search versus rewritten cache");
        Console.WriteLine($"Actual legacy search vs rewrite, controlled no-target fixture (100 workers, 600 ticks, 500 pawns): {Fakes.LegacyChecks:N0} vs {Fakes.TargetChecks:N0} eligibility checks; {baselineMs:F2} ms vs {watch.Elapsed.TotalMilliseconds:F2} ms. Low-level eligibility is stubbed equally; this measures search overhead, not colony TPS.");
    }
    static void TestCompiledContract()
    {
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(typeof(ComfortSession).Assembly.Location);
        var utility = module.Types.Single(t => t.FullName == "ComfortDuty.ComfortUtility");
        var extract = utility.Methods.Single(m => m.Name == "ExtractAt");
        Check(extract.Body.Instructions.Any(i => i.Operand is Mono.Cecil.MethodReference m && m.DeclaringType.FullName == "ComfortDuty.PlacementReceipt" && m.Name == "Record"), "real extraction wires callback accounting");
        var driver = module.Types.Single(t => t.FullName == "ComfortDuty.JobDriver_ComfortExtract");
        var refs = driver.NestedTypes.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Select(i => i.Operand).OfType<Mono.Cecil.MethodReference>().ToList();
        Check(!refs.Any(m => m.Name is "ReadyToExtract" or "AnyoneNeedsRelief" or "CanStart"), "extraction job has no population/reachability scan in tick failure checks");
        var session = module.Types.Single(t => t.FullName == "ComfortDuty.ComfortSession");
        Check(!session.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is Mono.Cecil.MethodReference m && m.Name == "get_Maps"), "sessions never scan game maps");
    }
}

internal static class Fakes
{
    internal static ComfortSession Current;
    internal static TickManager Manager;
    internal static Map FixtureMap = (Map)RuntimeHelpers.GetUninitializedObject(typeof(Map));
    internal static readonly HashSet<Pawn> Pawns = new(ReferenceEqualityComparer.Instance);
    internal static int Now, RewardCalls, Suppressed, TargetChecks;
    internal static bool Throw, NestedWasSuppressed, NoEligible;
    internal static int LegacyChecks;
    internal static List<Hediff> PartList;
    internal static SexProps Nested;
    internal static bool Session(ref ComfortSession __result) { __result = Current; return false; }
    internal static bool Clock(ref TickManager __result) { __result = Manager; return false; }
    internal static bool Ticks(ref int __result) { __result = Now; return false; }
    internal static bool Spawned(Thing __instance, ref bool __result) { if (__instance is not Pawn p || !Pawns.Contains(p)) return true; __result = true; return false; }
    internal static bool Map(Thing __instance, ref Map __result) { if (__instance is not Pawn p || !Pawns.Contains(p)) return true; __result = FixtureMap; return false; }
    internal static bool Dead(Pawn __instance, ref bool __result) { if (!Pawns.Contains(__instance)) return true; __result = false; return false; }
    internal static bool Adult(Pawn pawn, ref bool __result) { if (!Pawns.Contains(pawn)) return true; __result = true; return false; }
    internal static bool Rewards() { RewardCalls++; return false; }
    internal static bool Cheating(ref bool __result) { __result = false; return false; }
    internal static bool Target(Pawn pawn, ref bool __result) { TargetChecks++; __result = !NoEligible && pawn.thingIDNumber % 3 == 0; return false; }
    internal static bool Parts(ref List<Hediff> __result) { __result = PartList; return false; }
    internal static bool LegacyInvalid(ref bool __result) { LegacyChecks++; __result = false; return false; }
    internal static bool Skip() => false;
    internal static bool False(ref bool __result) { __result = false; return false; }
    internal static IEnumerable<CodeInstruction> ProcessBody(IEnumerable<CodeInstruction> instructions)
    {
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Fakes), nameof(Process)));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Process(SexProps props)
    {
        if (Throw) throw new InvalidOperationException("fixture failure");
        ThoughtDef thought = null;
        bool allowed = Patch_ThinkAboutSexConsensual.Prefix(props.pawn, props.partner, ref thought);
        if (!allowed) Suppressed++;
        if (Nested != null)
        {
            var nested = Nested; Nested = null;
            int previous = Suppressed; SexUtility.ProcessSex(nested);
            NestedWasSuppressed = Suppressed != previous;
        }
    }
}
