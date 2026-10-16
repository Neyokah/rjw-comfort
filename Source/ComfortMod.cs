using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ComfortDuty;

public sealed class ComfortSettings : ModSettings
{
    public float fallbackUnits = 1f, unitsBeforeExtraction = 1f;
    public override void ExposeData()
    {
        Scribe_Values.Look(ref fallbackUnits, "fallbackUnits", 1f);
        Scribe_Values.Look(ref unitsBeforeExtraction, "unitsBeforeExtraction", 1f);
        Sanitize();
    }
    internal void Sanitize()
    {
        fallbackUnits = float.IsFinite(fallbackUnits) ? Mathf.Clamp(fallbackUnits, 0f, 5f) : 1f;
        unitsBeforeExtraction = float.IsFinite(unitsBeforeExtraction) ? Mathf.Clamp(unitsBeforeExtraction, 1f, 10f) : 1f;
    }
}

public sealed class ComfortMod : Mod
{
    public static ComfortSettings Settings;
    internal static Action<Pawn, Pawn> AddCheaterThought;
    public ComfortMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ComfortSettings>();
        var harmony = new Harmony("umrae.rjw.comfort");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        var method = AccessTools.Method(typeof(InteractionWorker_RomanceAttempt), "TryAddCheaterThought", new[] { typeof(Pawn), typeof(Pawn) });
        if (method == null) Log.Warning("[RJW Comfort] Optional relationship helper unavailable; using memory fallback.");
        else
        {
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(Patch_TryAddCheaterThought), nameof(Patch_TryAddCheaterThought.Prefix)));
                AddCheaterThought = AccessTools.MethodDelegate<Action<Pawn, Pawn>>(method);
            }
            catch (Exception e) { Log.Warning("[RJW Comfort] Optional relationship integration unavailable: " + e.Message); }
        }
    }
    public override string SettingsCategory() => "RJW Comfort";
    public override void DoSettingsWindowContents(Rect rect)
    {
        var listing = new Listing_Standard();
        listing.Begin(rect);
        Settings.fallbackUnits = listing.SliderLabeled("Comfort_FallbackUnits".Translate(Settings.fallbackUnits.ToString("0.##")), Settings.fallbackUnits, 0f, 5f, 0.65f, null);
        listing.Gap(8f);
        Settings.unitsBeforeExtraction = listing.SliderLabeled("Comfort_UnitsBeforeExtraction".Translate(Settings.unitsBeforeExtraction.ToString("0.##")), Settings.unitsBeforeExtraction, 1f, 10f, 0.65f, null);
        Settings.Sanitize(); listing.End();
    }
}
