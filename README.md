# RJW Comfort

**Version 1.1.0 · RimWorld 1.7 · Package ID `umrae.rjw.comfort`**

An adult-only Comfort work system built around RJW's native jobs, eligibility checks, mutual preferences, and completion events. It also collects the supported resource output and provides optional ideology and Cumpilation integration.

## Features

- A **Comfort** work type with need-relief work and lower-priority collection work. Enable it in the Work tab for suitable pawns.
- Eligibility requires humanlike adult pawns, biological age **18 or older**, the adult RJW age category, and an adult developmental stage. Native RJW eligibility, mutual preference, job availability, and reservation rules still apply.
- Ideology support offers **accepted**, **encouraged**, or **counts as cheating** treatment for Comfort work. The definitions are conditional on Ideology.
- Reuses RJW's existing completion pipeline. Other listeners receive the native event; Comfort does not replay the pipeline.
- Exact-job session tracking prevents repeated rewards and clears interrupted or removed jobs.
- Collection accounts for what was actually placed. Fractions and quantities that could not be placed remain stored.
- Shared, lazy per-map selection caches avoid repeated population searches. Candidates can take up to **120 ticks** to refresh; eligibility is checked again when starting a job.

## Installation

1. Install the required DLC and dependency mods listed below.
2. Download this repository using **Code → Download ZIP**, then extract it. The compiled mod is included; building is optional.
3. Put the extracted folder in `RimWorld/Mods/`. You may rename it to `rjw-comfort`. `About/About.xml` must sit directly inside that mod folder, rather than an extra nested folder.
4. Keep only one enabled copy of this mod. Enable it in RimWorld's Mods menu after its dependencies and the supported mods you use.
5. Restart RimWorld after installation or updating.

For Steam, **Manage → Browse local files** opens the game installation. On macOS the usual Mods location is `~/Library/Application Support/Steam/steamapps/common/RimWorld/Mods/`.

## Dependencies and integrations

| Mod | Status | Integration |
| --- | --- | --- |
| Harmony | Required | Runtime patches. |
| RimJobWorld | Required | Native jobs, preferences, eligibility, part/fluid data, and completion hooks. |
| Cumpilation | Optional | Uses a matching gathering definition's output item and conversion ratio. A standalone output remains available when no matching definition exists. |
| Ideology DLC | Optional | Comfort precepts and their associated treatment/thoughts. |
| RJW Stack Compatibility | Separate optional mod | Handles different generation/lookup problems; Comfort does not replace it. |

Load after Harmony, RJW, and Cumpilation if installed. No blanket support claim is made for every race, body model, or RJW add-on.

## Settings

Open **Options → Mod settings → RJW Comfort**.

| Setting | Range | Default | Meaning |
| --- | --- | --- | --- |
| Fallback resource units | 0–5 | 1 | Used only when a resource measurement is unavailable; a valid measured zero stays zero. |
| Units before extraction | 1–10 | 1 | Minimum stored amount before collection work is considered. |

## Updating and saves

Keep a backup save before changing an existing mod stack. The package ID, definition names, stored resource, and settings keys remain stable. Replace the old folder instead of enabling two copies.

Legacy 1.0 session markers lack a native-job identity and are discarded during load. Existing jobs and stored resources remain; an already-running old session will not receive Comfort rewards from its discarded marker. New sessions use exact-job tracking. Do not assume a save written by 1.1 is suitable for downgrading to 1.0. Removing a content mod from an ongoing save is not certified here.

## Verification status

The original controlled integration run recorded **21,547 checks**. Coverage includes adult eligibility, native patch targets, partial placement, interruption cleanup, scoped completion handling, and actual Scribe round trips. Its isolated search benchmark is not a full gameplay or TPS measurement. See `VALIDATION.txt` for fixture limits. Multiplayer synchronization and full-colony play have not been certified.

## Source, testing, and support

- [Build instructions](BUILDING.md)
- [Compatibility details](COMPATIBILITY.md)
- [Changelog](CHANGELOG.md)
- [Contribution guidelines](CONTRIBUTING.md)
- [Report a bug](https://github.com/Neyokah/rjw-comfort/issues/new?template=bug_report.yml)

These builds target **RimWorld 1.7's .NET 10 runtime**. They are not RimWorld 1.6 binaries. Older dependency assemblies used by the installed 1.7 compatibility runtime do not establish that the mod itself supports 1.6. The original automated test records are included where available; full graphical gameplay with a large mod collection is not claimed to have been verified.

## Credits and license

Original code in this project is available under the [MIT license](LICENSE). Third-party dependencies retain their own authorship and terms and are not bundled. See [third-party notices](THIRD_PARTY_NOTICES.md) for any exceptions or adapted material.
