# Changelog

1.1.0
- Replaced repeated map searches with a shared lazy snapshot and worker caches.
- Reused native RJW job, preference, eligibility and completion facilities.
- Added optional Cumpilation item/conversion definition integration.
- Bound session tracking to exact job IDs and added interruption cleanup.
- Scoped thought/cheating changes to the active Comfort completion only.
- Added save/load rebinding and migration of legacy session markers.
- Prevented duplicate completion rewards and corrected partial-placement debit.
- Replaced life-stage name matching with explicit adult eligibility checks.
- Preserved package ID, definition names, stored resources and settings keys.
- Added an explicit build for the installed RimWorld 1.7 runtime.

## Repository preparation
- Added public installation, compatibility, build, contribution, and issue-reporting documentation.
- Replaced creator-specific build/test paths with configurable paths. Gameplay source is unchanged.
