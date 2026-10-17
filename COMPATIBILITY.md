# Compatibility

## Supported target

RimWorld **1.7**, targeting its .NET 10 runtime. The source/binaries were prepared against a local macOS compatibility-runtime installation. Other operating systems, future game builds, and every possible mod combination are not separately certified. Obtain the required dependencies independently; they are not bundled.

## Integration scope

Cumpilation support reads matching gathering definitions, their output item, and conversion ratio. Without one, standalone collection remains available. Ideology definitions are conditional and not a hard dependency. Native RJW events remain native; changing a dependency's event/job signatures can require a new adapter. Multiplayer, every race, and every body/animation pack are not certified. The separate Stack Compatibility mod solves different issues.

## Submission integration (1.2.0)

Patches the installed RJW `SubmitGizmo` constructor and `LayDownAndAccept(Pawn)` action. Uses the native `Hediff_Submitting` and its disappearing component. Mods replacing those signatures, removing that component, or independently changing the same button may need an adapter. Native carrying/health removal remains in effect; this mod does not globally extend NPC or other scripted submission. The existing synchronized action is retained; multiplayer is not certified.

## Load-order hints from the shipped metadata

The following are placement hints, **not a list of required dependencies or a blanket compatibility guarantee**. Required dependencies are separately listed in README.md and About/About.xml.

- `brrainz.harmony`
- `rim.job.world`
- `vegapnk.cumpilation`

## Reporting an integration problem

Report the exact game and dependency versions, whether the problem reproduces with only required dependencies, and a minimal reproduction. Preserve unrelated load-order rules from upstream mods. See the included verification records for what has actually been tested.
