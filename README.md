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

1. Drag `Packages/Ceffy/Runtime/Prefabs/CeffyScreenSpace.prefab` into a scene.
2. Set **Start Url** on its `WebBrowser` component.
3. Use a normal URL or `streaming-assets://path/to/index.html` for content under `Assets/StreamingAssets`.
4. Add `using Ceffy;` when accessing Ceffy components from C#.

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
