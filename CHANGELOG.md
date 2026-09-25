# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed (breaking)

- Renamed `WebBrowser` to `CeffyInstance` and `WebBrowserCanvasDisplay` to `CeffyInstanceView`. Code must use the new names.
- `CeffyInstance` adds `RawImage` and `CeffyInstanceView` at runtime (and a Canvas when not under one), so scenes and prefabs only need the `CeffyInstance` component. The view always displays the instance on its own GameObject; the `webBrowser` field is gone.
- `CeffyScreenSpace.prefab` is now a single GameObject holding the Canvas and `CeffyInstance` (display components are added at runtime).
- `CeffyBridge` takes a `CeffyInstance`.
- When the `RawImage` is a raycast target, a view only receives the pointer while no other UI element is on top of it.

### Added

- **Use Shared Instance** toggle on `CeffyInstance`: runs the page as an isolated iframe inside one browser shared by all shared instances, rendering to its own region of a single texture. Messaging, JavaScript, navigation, resizing, console messages, and mouse/keyboard input work as for dedicated instances; zoom is not supported. Atlas size is set via `CeffyInstance.SharedAtlasWidth`/`SharedAtlasHeight`.
- Shared Instance Demo sample (orbiting nameplates + interactive panel using shared instances, with a `DemoFollowTarget` helper). Requires URP for the cube materials.

## [1.0.1] - 2026-09-23

### Fixed

- Corrected `WebBrowserCanvasDisplay` creating a `RawImage` only when one is missing.
- Updated README to include openUPM badge

## [1.0.0] - 2026-09-23

### Added

- Initial public release