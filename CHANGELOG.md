# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0](https://github.com/hovgaardgames/ceffy/compare/v1.3.0...v1.4.0) (2026-10-07)


### Features

* add Asset Store release preparation ([#19](https://github.com/hovgaardgames/ceffy/issues/19)) ([e60e47b](https://github.com/hovgaardgames/ceffy/commit/e60e47bd582ab01fb81306db0e85a8004e8e71f1))
* generate inherited types in TypeScript generation ([#18](https://github.com/hovgaardgames/ceffy/issues/18)) ([c3ad0cc](https://github.com/hovgaardgames/ceffy/commit/c3ad0cc395447fd6b5d8cf8d1200cfd274eb416d))


### Bug Fixes

* skip ignored fields in TypeScript generation ([#18](https://github.com/hovgaardgames/ceffy/issues/18)) ([c3ad0cc](https://github.com/hovgaardgames/ceffy/commit/c3ad0cc395447fd6b5d8cf8d1200cfd274eb416d))

## [Unreleased]

## [1.3.0](https://github.com/hovgaardgames/ceffy/compare/v1.2.0...v1.3.0) (2026-10-01)

### Features

* Add gameplay HUD and world-space UI demo ([#16](https://github.com/hovgaardgames/ceffy/issues/16)) ([a98ddb1](https://github.com/hovgaardgames/ceffy/commit/a98ddb1a6e4392ae5672bdc8f95dfafbf21e8326))

### Bug Fixes

* UPM validation errors and warnings, normalized script line endings, and reset static variables. ([#15](https://github.com/hovgaardgames/ceffy/issues/15)) ([ff0a4d7](https://github.com/hovgaardgames/ceffy/commit/ff0a4d7acc65ee3474b9378998576e4c7cb69f75))

## [1.2.0](https://github.com/hovgaardgames/ceffy/compare/v1.1.1...v1.2.0) (2026-09-29)

### Features

* Add shared-instance mode and rename WebBrowser to CeffyInstance ([#6](https://github.com/hovgaardgames/ceffy/issues/6)) ([83daa75](https://github.com/hovgaardgames/ceffy/commit/83daa75d14a0f7f64b6b9ce5d0df9c91915e19db))
* Add world-space Ceffy sample ([#7](https://github.com/hovgaardgames/ceffy/issues/7)) ([0f021dc](https://github.com/hovgaardgames/ceffy/commit/0f021dcc6a8a16de006d5c7d42c228978d0fe038))
* Adds linting to the project ([#9](https://github.com/hovgaardgames/ceffy/issues/9)) ([7923be1](https://github.com/hovgaardgames/ceffy/commit/7923be1039da7f5d0f7532b46a8c82cfdf867255))

### Bug Fixes

* Correct washed-out browser UI at non-native fullscreen resolutions ([#10](https://github.com/hovgaardgames/ceffy/issues/10)) ([79e20f7](https://github.com/hovgaardgames/ceffy/commit/79e20f7e27f22ee858f37d5f19558488f4ce0e2f))
* Exclude orphaned meta files in npm ignore ([#13](https://github.com/hovgaardgames/ceffy/issues/13)) ([8f10261](https://github.com/hovgaardgames/ceffy/commit/8f10261acaf1b58e5e1b924f21cee3e76dfee505))
* Mouse wheel scrolling not working when using the new input system ([#8](https://github.com/hovgaardgames/ceffy/issues/8)) ([38088a4](https://github.com/hovgaardgames/ceffy/commit/38088a45037ba3c68c2f937dfab0d3e18ae1a0e1))

## [1.1.1](https://github.com/hovgaardgames/ceffy/compare/v1.1.0...v1.1.1) (2026-09-25)

### Bug Fixes

* Fixed documentation section not generating changelog entry ([#4](https://github.com/hovgaardgames/ceffy/issues/4)) ([aadd225](https://github.com/hovgaardgames/ceffy/commit/aadd225149bebb343334984bdd9504b036441667))

### Documentation

* Update readme sample image and added instructions for openUPM install ([#3](https://github.com/hovgaardgames/ceffy/issues/3)) ([455cbe6](https://github.com/hovgaardgames/ceffy/commit/455cbe624d0d110e65d8852154dcf8e061da49f2))

## [1.1.0](https://github.com/hovgaardgames/ceffy/compare/v1.0.1...v1.1.0) (2026-09-25)

### Features

* Add first version of CI release ([#1](https://github.com/hovgaardgames/ceffy/issues/1)) ([a63718e](https://github.com/hovgaardgames/ceffy/commit/a63718e1bdeca8467a5d1e4f41b34c09f2186eef))

## [1.0.1] - 2026-09-23

### Fixed

- Corrected `WebBrowserCanvasDisplay` creating a `RawImage` only when one is missing.
- Updated README to include openUPM badge

## [1.0.0] - 2026-09-23

### Added

- Initial public release
