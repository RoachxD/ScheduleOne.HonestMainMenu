# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Colorized the main menu avatar by loading saved clothing entries, replaying their colorized layers/accessories, and populating `ClothingUtility` with the curated palette or extracted live data.
- Captured live clothing colors whenever leaving the menu scene so updated palettes persist between sessions.

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

[Unreleased]: https://github.com/RoachxD/ScheduleOne.HonestMainMenu/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/RoachxD/ScheduleOne.HonestMainMenu/releases/tag/v1.0.1
[1.0.0]: https://github.com/RoachxD/ScheduleOne.HonestMainMenu/releases/tag/v1.0.0
