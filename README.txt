RJW Comfort 1.2.0
Package: umrae.rjw.comfort

This replacement targets the installed RimWorld 1.7 runtime. It requires Harmony
and RimJobWorld. Cumpilation is optional. Load it after RJW and, when installed,
Cumpilation. Keep only one copy of RJW Comfort enabled.

Submit toggle (1.2.0)
Enable the Submit button in RJW settings and select a drafted eligible adult colonist.
Submit (24h) starts one in-game day (60,000 ticks). Click End submission to stop early.
The cancellation button remains available after undrafting. Remaining time is saved
by RJW's normal disappearing hediff component and pauses when the game is paused.
Other sources of submission keep their original durations. RJW's existing health
and removal rules still apply, including same-faction carrying. Already-active
states keep their timer; cancel and reactivate to start a new full day.

Reuse of installed mods
- Uses RJW's native quick job, eligibility checks, orientation/anatomy preference
  calculation and completion pipeline. Other mods continue to receive the native
  RJW event; Comfort does not replay that pipeline.
- Reads RJW's existing part/fluid data. If Cumpilation supplies a matching
  gathering definition, uses its output item and conversion ratio.
- Retains the existing Comfort work type, precepts, thoughts, stored-resource
  definition and settings keys. Standalone output remains available without
  Cumpilation. A valid measured zero produces zero; the configured fallback is
  reserved for unavailable resource measurements.

Performance and reliability
- Population filtering is shared per map and refreshed lazily at most once per
  120 ticks, or immediately when the population count changes. Worker decisions
  and storage searches are cached within that interval. Newly eligible targets
  can take up to 120 ticks to appear. Eligibility is checked again before a job
  begins; a cached decision does not authorize an interaction.
- Travel uses cheap validity checks. Extraction no longer repeats a whole-map
  relief search every tick. No new map ticker or background population scan.
- Sessions bind to the exact native job. Interruption, queue removal and failed
  completion clear the session; repeated callbacks cannot award twice.
- Event overrides apply only within the matching Comfort completion. Unrelated
  and nested events retain their normal behavior.
- Resource accounting deducts the quantity actually placed, including partial
  stack merges. Fractions and unplaced quantities remain stored.
- Eligibility requires adult humanlike pawns, biological age at least 18, an
  adult RJW age category and an adult developmental stage. Native RJW eligibility
  and mutual preference checks still apply.

Updating and saves
Replace the existing rjw-comfort folder; do not install a second enabled copy.
Restart RimWorld to load the replacement assembly. The package ID is unchanged,
so the existing enabled entry and mod settings remain in place.

Old 1.0 session markers did not record their associated native job. They are
discarded on loading an old save. Existing jobs and stored resources are not
removed; an already-running old interaction will not receive Comfort rewards
from its discarded marker. Newly started sessions use the new tracking format.
Keep a pre-update save if you want to return to 1.0. The original 1.0 mod is not bundled in this repository.

Validation
See VALIDATION.txt and test-results.txt. Tests use the installed game/RJW runtime
with controlled fixtures. Full colony gameplay and colony TPS were not measured;
the isolated search benchmark is not a promise of an overall speed multiplier.
This does not establish compatibility with every installed mod or race, and it
does not replace the separate RJW Stack Compatibility patch.

Source
Source/ contains the rewrite. Build with the .NET 10 SDK. GameDir, RjwDir and
HarmonyPath are configurable MSBuild properties; defaults point to this Mac's
installed copies. Referenced game/mod assemblies are not bundled in this mod.
