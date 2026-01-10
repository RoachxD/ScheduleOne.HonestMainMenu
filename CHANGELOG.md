# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-01-10

### Added

- Colorized the main menu avatar by loading saved clothing entries, replaying their colorized layers/accessories, and populating `ClothingUtility` with the curated palette or extracted live data.
- Captured live clothing colors whenever leaving the menu scene so updated palettes persist between sessions.
- Added embedded default clothing color data and serialization models to support reading/writing clothing save data and palettes.
- Implemented registry/resource fallbacks and ItemDefinition mapping to resolve clothing definitions when saves reference items missing from the registry.
- Added IL2CPP build support for PlayerClothing-based application of saved outfits (prevents AvatarSettings crashes) and referenced Il2CppFishNet runtime where required.
- Introduced utility helpers for menu child lookups and centralized continue popup error handling.

### Changed

- Optimized clothing load pipeline: pooled/cached clothing JSON parsing, in-place prompt pruning, and single-attach menu hooks.
- Avoided duplicate main menu rig `LoadStuff` execution by tracking rigs per scene and resetting between scene loads.
- Streamlined clothing data IO and caching: hash-checked color data to skip reloads/writes, cached embedded palettes, and seeded IL2CPP color data before use.

### Fixed

- Prevented IL2CPP crashes when applying clothing by routing through `PlayerClothing` instances and pre-seeding `ClothingUtility` colors.
- Improved resilience when registry entries are missing definitions by falling back to register/resource definitions or mapping ItemDefinitions to avatar assets.

## [1.0.1] - 2025-11-15

### Fixed

- Updated the continue flow to prefer the new `LoadManager.StartGame` overload and gracefully fall back to the legacy signature when it is missing (e.g. on older game builds).
- Added logging and a user-facing popup when continuing the last played game fails, instead of failing silently.

## [1.0.0] - 2025-06-14

### Added

- Initial release of **Honest Main Menu**.
- Introduced a true "Continue" button that directly loads the most recent game session.
- Repurposed the game's original "Continue" button to function as a "Load Game" button, including a label change to "Load Game".
- Updated the title of the save selection screen (accessed via the new "Load Game" button) from "Continue" to "Load Game" for clarity.
- Corrected the main menu's back button UI prompt by removing the misleading "RMB" (Right Mouse Button) indicator, as only the Escape key is functional here.
- Implemented a Harmony patch ([`Patches.SceneManagerLoadScenePatch`](Patches/SceneManagerLoadScenePatch.cs)) for `SceneManager.LoadScene` to prevent the "Menu" scene (and others) from loading multiple times consecutively, addressing issues observed on startup and when returning to the main menu from a game session.
- Provided dual build support for both IL2CPP and Mono versions of the game.
- Configured buttons to be interactable only if save games exist.

[1.1.0]: https://github.com/RoachxD/ScheduleOne.HonestMainMenu/releases/tag/v1.1.0
[1.0.1]: https://github.com/RoachxD/ScheduleOne.HonestMainMenu/releases/tag/v1.0.1
[1.0.0]: https://github.com/RoachxD/ScheduleOne.HonestMainMenu/releases/tag/v1.0.0
