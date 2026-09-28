<p align="center">
<img src="Documentation~/ceffy_logo_social_github.png" alt="Embeds a CEF (Chromium Embedded Framework) browser inside Unity via a native C++ plugin. Distributed as a UPM package with Git LFS–tracked native binaries." width="960">
</p>

# Ceffy

[![Unity 6+](https://img.shields.io/badge/Unity-6.0%2B-black?logo=unity)](https://unity3d.com/get-unity/download)
[![License: MIT](https://img.shields.io/badge/License-MIT-brightgreen.svg)](LICENSE.md)
[![openupm](https://img.shields.io/npm/v/com.hovgaard.ceffy?label=openupm&registry_uri=https%3A%2F%2Fpackage.openupm.com)](https://openupm.com/packages/com.hovgaard.ceffy/)

Embeds a CEF (Chromium Embedded Framework) browser inside Unity via a native C++ plugin. Distributed as a UPM package with Git LFS–tracked native binaries.

## Requirements

- **Unity 6000.0+** (Unity 6)
- **Windows x64** (other platforms not yet supported)
- **Git LFS** enabled on the client that clones/installs the package

## Installation

### Via OpenUPM (Package Manager)

1. Open **Edit > Project Settings > Package Manager** in Unity.
2. Add a scoped registry with **Name** `OpenUPM`, **URL** `https://package.openupm.com`, and **Scope** `com.hovgaard.ceffy`, then click **Apply**.
3. Open **Window > Package Manager**.
4. Click **+ > Add package by name**, enter `com.hovgaard.ceffy`, and click **Add**.

### Via Git URL (Package Manager)

1. Open **Window > Package Manager**
2. Click **+** > **Install package from git URL…**
3. Paste:

```
https://github.com/hovgaardgames/ceffy.git
```

For a stable release, pin the package to a version tag:

```text
https://github.com/hovgaardgames/ceffy.git
```

If the native runtime fails to load after a Git installation, confirm that Git LFS is installed and that the downloaded binaries are not LFS pointer files.

## Quick Start

1. Add a `CeffyInstance` component to a GameObject. At runtime it adds the `RawImage` and `CeffyInstanceView` it renders through, and becomes its own screen-space canvas if it isn't already under one. Alternatively, drag `Packages/Ceffy/Runtime/Prefabs/CeffyScreenSpace.prefab` into a scene for a full-screen setup.
2. Set **Start Url** on the `CeffyInstance`.
3. Use a normal URL or `streaming-assets://path/to/index.html` for content under `Assets/StreamingAssets`.
4. Add `using Ceffy;` when accessing Ceffy components from C#.

## Shared Instances

Enable **Use Shared Instance** on a `CeffyInstance` when you have many small UI elements (nameplates, labels, tooltips, context menus) and don't want one browser per element.

Every shared instance runs as its own iframe inside a single shared browser and renders into its own region of one shared texture. Unity still owns each element's position, and the page gets the same `window.ceffy` API as in a dedicated instance, so the same page works in either mode.

- Size the element with **Width**/**Height** and turn off **Auto Resize To Rect Transform** for world-anchored elements; their on-screen size changes constantly and each change would move them in the shared texture.
- You can call `SendToCeffy` and `ExecuteJS` right away. Both wait for the page to load, and a message also waits until the page sets `window.ceffy.onMessageFromUnity`. Calling `Navigate` drops anything still waiting, since it belonged to the previous page.
- For world-anchored UI, project the element onto a screen-space canvas each frame (see the Shared Instance Demo's `DemoFollowTarget`).
- Zoom is not supported. JavaScript execution and messaging require pages from the same origin as the shared host, which holds for `streaming-assets://` and `file://` pages.
- The shared texture defaults to 2048×2048; set `CeffyInstance.SharedAtlasWidth`/`SharedAtlasHeight` before the first shared instance is enabled to change it. Instances that don't fit log an error and stay blank.
- Shared pages are not sandboxed from each other. They get separate iframes, but they run in one browser with web security turned off, so any shared page can reach the others. Put untrusted or third-party content in a dedicated instance.

Use a transparent page background and lay the page out at the instance's Width/Height.

## Samples

Optional demos are available under **Package Manager > Ceffy > Samples**:

![Available Ceffy samples in Unity Package Manager](Documentation~/samples.png)

After importing a sample, the included `CeffySampleStreamingAssets` editor script automatically copies its HTML files to `Assets/StreamingAssets/`.

## Development

### Package Layout

- `Runtime/`, `Editor/`, and `NativeRuntime/` contain the installed package.
- `Samples~/` contains optional samples that users can import through Package Manager.
- `native~/` contains development-only CMake, C++, and setup files and is not imported by Unity.

### Local Package Development

1. Create or open a Unity project.
2. Open **Window > Package Manager**.
3. Select **+ > Install package from disk...**.
4. Select this repository's `package.json`.

The package appears under **Packages/Ceffy**. Changes to `Runtime/` and `Editor/` are available immediately through the local package reference.

### Native Build

Native development requires:

- **CMake 3.21+**
- **Visual Studio 2022** with the **Desktop development with C++** workload

Build and stage the native runtime with:

```powershell
.\native~\build-native.ps1
```

If CEF is missing, the script downloads and configures it automatically. Output is staged to `NativeRuntime/win-x64/`.

To download and configure CEF without building, run:

```powershell
.\native~\setup.ps1
```

## Testing

Add `"testables": ["com.hovgaard.ceffy"]` to the consuming Unity project's `Packages/manifest.json`, then open **Window > General > Test Runner**. EditMode contains unit tests; PlayMode contains the Windows native runtime smoke test.

## License

Ceffy is available under the [MIT License](LICENSE.md). Third-party components retain their respective licenses as listed in [THIRD PARTY NOTICES](THIRD%20PARTY%20NOTICES.md).
