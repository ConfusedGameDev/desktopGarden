# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Pollen Garden** — an ambient desktop idle game. A transparent, always-on-top, click-through Unity window renders a 3D flower over the user's desktop; the player clicks petals to harvest pollen while working. Repo/product name is still `DesktopGarden`; the game name is Pollen Garden.

- Unity **6000.7.0a3** (alpha), **URP 17.7**, new Input System only (`activeInputHandler: 1`)
- Lead platform **macOS**, then Windows → iOS/Android → visionOS/Quest. **One Unity project for every target** — no forks, no per-platform branches.
- **`Plan.md` at the repo root is the source of truth** for design, architecture, and milestones. Read it before making architectural decisions. When a milestone checkbox in §5 is genuinely satisfied, mark it `[x]` in `Plan.md` as part of that work.

### Current state

Early. The flower **geometry + assembly** slice is built and verified (`Assets/01.Scripts/Flowers/`, 13 green EditMode tests); everything else — HP, clicking, sway, helpers, economy, save, and the whole M0 window layer — is unbuilt. The only scene is still the template `Assets/Scenes/SampleScene.unity`.

### The Flowers module

Two assemblies exist: `CONFUSEDGAMEDEV.PollenGarden.Flowers` and `CONFUSEDGAMEDEV.PollenGarden.EditMode.Tests`. There is deliberately **no `Core` asmdef yet** — create it with the first `.Core` file and set `noEngineReferences: true` then, when it can actually enforce something.

- `PetalShapeParameters` — pure silhouette math, no `Mesh`, no scene deps. The EditMode-testable nucleus.
- `PetalMeshBuilder` — static generator. Petal is authored **XY plane, z = 0, base at origin, growing +Y, facing −Z**. Base-at-origin is load-bearing: it puts each petal's hinge at its own base, which is what sway (local Z) and droop (local X) will need. Do not make it centre-pivoted.
- `FlowerSpeciesData` — ScriptableObject; all geometry/colour tuning. Gameplay fields append here in M1.
- `FlowerController` — `[ExecuteAlways]`, sole owner of the 2 meshes + 2 materials, `[ContextMenu("Rebuild Flower")]`.
- `PetalController` — thin view component; the anchor M1 attaches HP, colliders, and per-petal tint to.

Three invariants worth not breaking:

1. **`sharedMesh`/`sharedMaterial` only** — `MeshFilter.mesh` and `Renderer.material` clone on every access; that is the classic procedural-mesh leak. The leak check in verification (rebuild ×3, assert exactly 2 `PG_` meshes) exists to catch exactly this.
2. **Ownership is recovered from the scene graph, not from fields.** `Rebuild()` sweeps children named `PG_Petal_*`/`PG_Center` and destroys their non-persistent meshes/materials. `HideFlags.DontSave` objects cannot be serialized, so fields pointing at them do not survive a domain reload — but the child GameObjects do, and they hold the references.
3. **Meshes carry normals even though the shader is unlit.** SSAO is enabled with `Source: DepthNormals` (`Assets/Settings/PC_Renderer.asset:78`) and URP's `UnlitDepthNormalsPass.hlsl` reads `NORMAL`. Tangents are read but unused, so they are omitted.

Per-species tint uses a **cloned shared material**, not `MaterialPropertyBlock` — MPB breaks SRP Batcher compatibility and excludes renderers from the GPU Resident Drawer, which is enabled here (`m_GPUResidentDrawerMode: 1`). `Plan.md` §3 was corrected accordingly.

Unity 6.7 ships a `UAL0010`/`UAL0013` analyzer requiring every static field to carry `[AutoStaticsCleanup]` or `[NoAutoStaticsCleanup]` (`Unity.Scripting.LifecycleManagement`). Annotate new statics or the build warns.

### Odin Inspector

Odin lives at `Assets/Plugins/Sirenix/` as precompiled DLLs (no asmdefs). Because the Flowers asmdef has `overrideReferences: false`, `Sirenix.OdinInspector.Attributes` is auto-referenced — runtime code can use `[InlineEditor]`, `[Button]`, etc. directly. `FlowerController` uses both (species field, `Rebuild()`).

**Known incompatibility:** the currently imported Odin build throws `MissingFieldException: UIElementsUtility.s_BeginContainerCallback` from its `InitializeOnLoad` on Unity 6000.7.0a3 (the alpha removed that internal API), and its DLL `.meta` files predate PluginImporter v2. Attributes compile and are harmless; Odin's *drawing* may fail until Odin is updated for this Unity version. Keep a `[ContextMenu]` fallback beside every Odin `[Button]` so functionality never depends on Odin rendering.

### Asset folder layout

Numbered top-level buckets under `Assets/`, ordered by pipeline stage:

| Folder | Holds |
|---|---|
| `00.Plugins/` | Native plugins — `macOS/` (the Swift `.bundle`), `Windows/` |
| `01.Scripts/` | All C#. Subfolders mirror the sub-namespaces: `Core Flowers Helpers Garden Economy Platform UI`, plus `Editor/` |
| `02.Graphics/` | Meshes, materials, shaders, textures |
| `03.Audio/` | Clips, mixers |
| `04.Prefabs/` | Prefabs |
| `05.Data/` | ScriptableObject instances (`FlowerSpeciesData`, `HelperData`, …) + `Input/` |
| `06.Timelines/` | Timeline/Playable assets |
| `07.UI/` | UI *assets* — UXML, USS, sprites, canvas prefabs. UI *code* lives in `01.Scripts/UI/` |
| `99.Testing/` | `EditMode/`, `PlayMode/` test assemblies, plus scratch/sample content |

`Assets/Scenes/` and `Assets/Settings/` stay unnumbered: both are Unity conventions referenced by GUID from `ProjectSettings/` (Graphics, Quality, Build Profiles).

Two consequences of the numbered scheme worth knowing:

- **`00.Plugins` is not Unity's magic `Assets/Plugins` folder.** The predefined `Assembly-CSharp-firstpass` behavior does not apply (irrelevant here — the plan uses asmdefs anyway), and `PluginImporter` will *not* auto-assign the platform for a native binary from the path. Set the target platform manually in the importer inspector when the macOS `.bundle` lands; the choice persists in its `.meta`.
- **Move assets with `AssetDatabase.MoveAsset`, not `mv`** — via the `unity-mcp` `Unity_RunCommand` tool while the Editor is open. It preserves GUIDs and keeps the asset database consistent. A shell `mv` that leaves a `.meta` behind silently breaks every reference to that asset.

## Commands

Unity editor binary (macOS):

```
/Applications/Unity/Hub/Editor/6000.7.0a3/Unity.app/Contents/MacOS/Unity
```

The editor holds an exclusive lock on `Library/`. **Close the Unity Editor before any batchmode command**, or it will fail/corrupt the import state.

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.7.0a3/Unity.app/Contents/MacOS/Unity
PROJ=/Users/jorgepedrero/Documents/Claude/desktopGarden

# EditMode tests (the simulation core must be testable here — no scene, no platform)
"$UNITY" -runTests -batchmode -projectPath "$PROJ" \
  -testPlatform EditMode -testResults "$PROJ/Logs/editmode.xml" -logFile -

# PlayMode tests
"$UNITY" -runTests -batchmode -projectPath "$PROJ" \
  -testPlatform PlayMode -testResults "$PROJ/Logs/playmode.xml" -logFile -

# A single test / fixture (NUnit filter, regex on full name)
"$UNITY" -runTests -batchmode -projectPath "$PROJ" -testPlatform EditMode \
  -testFilter "CONFUSEDGAMEDEV.PollenGarden.Economy.Tests.TradeTests.Trade_100Pollen_Yields1Nectar" \
  -testResults "$PROJ/Logs/one.xml" -logFile -

# Compile-check without running anything (fastest sanity check after edits)
"$UNITY" -quit -batchmode -projectPath "$PROJ" -logFile - | tail -40

# Player build (also available as menu item "Pollen Garden/Build/macOS Player")
"$UNITY" -quit -batchmode -projectPath "$PROJ" \
  -executeMethod CONFUSEDGAMEDEV.PollenGarden.Editor.BuildScript.BuildMac -logFile -
# Output: Builds/macOS/PollenGarden.app — player log at ~/Library/Logs/DefaultCompany/DesktopGarden/Player.log

# Rebuild the macOS window plugin after editing its source
Assets/00.Plugins/macOS/Source~/build.sh
```

Test results are JUnit-ish NUnit XML; `grep -E 'result="Failed"|<failure'` the file. Editor logs also land in `~/Library/Logs/Unity/Editor.log`.

### Working with the live editor

A `unity-mcp` server is connected. When the Editor is open, prefer it over batchmode for iteration:
`Unity_GetConsoleLogs` (compile errors + runtime warnings), `Unity_RunCommand`, `Unity_Camera_Capture` / `Unity_SceneView_Capture2DScene` (visual verification — essential for a game whose whole point is what it looks like composited over the desktop).

## Architecture

The shape below is prescribed by `Plan.md` §3 and should be honored as code lands.

### Layering

1. **Simulation core** — plain C#, zero `UnityEngine` scene/platform dependencies, EditMode-testable, identical on every platform. Economy, garden state, helper scheduling, offline computation live here.
2. **Presentation** — MonoBehaviours that render/animate what the core computes (flower assembly, helper agents, HUD).
3. **Platform** — everything OS-specific hides behind `IWindowPlatform`.

Systems (one responsibility each): `GameManager`, `EconomyManager`, `GardenManager`, `FlowerController`/`PetalController`, `HelperManager`, `HelperAgent`, `PowerupManager`, `SaveManager`, `UIManager`, `WindowModeManager`.

### `IWindowPlatform` — the critical seam

```
SetTransparent(bool) · SetClickThrough(bool) · SetAlwaysOnTop(bool) · SetWindowRect(...) · SetExpanded(bool)
```

- **MacWindowPlatform** (lead) — Swift/Obj-C `.bundle` at `Assets/Plugins/macOS/` exposing a C API, called via `[DllImport]`. `NSWindow.isOpaque=false`, clear `backgroundColor`, `ignoresMouseEvents` toggling, floating window level, `canJoinAllSpaces`, `CAMetalLayer.isOpaque=false`. Not a separate app — a normal Unity native plugin inside this project.
- **WindowsWindowPlatform** — pure P/Invoke into user32/dwmapi (`DwmExtendFrameIntoClientArea`, `WS_EX_LAYERED`, dynamic `WS_EX_TRANSPARENT`, `HWND_TOPMOST`). No compiled plugin, so no extra CI cost.
- **OpaquePlatform** — no-op for mobile/MR.

Platform divergence is allowed **only** in these implementations, `#if UNITY_*` blocks, `Assets/Plugins/<platform>/`, and Build Profiles.

### Two constraints that shape everything

- **URP post-processing destroys the window alpha channel.** The overlay is unusable if alpha is not preserved end-to-end: camera clears to alpha 0, minimal post-FX, alpha-preserving final blit. Validate this before building features on top of it (M0 exit criterion). Touching the render pipeline assets in `Assets/Settings/` (`PC_RPAsset`, `PC_Renderer`) requires re-checking it.
- **Click-through is per-frame.** The UI layer publishes the union of interactive screen rects (petals, HUD, Expand button) to the platform layer each frame; cursor inside any rect → click-through off, outside → on, with a few-pixel hysteresis to stop edge flicker. Any new interactive element must register its rect or it will be unclickable.

### Flower construction

One authored petal mesh (~100–200 tris) assembled **procedurally at runtime** from `FlowerSpeciesData` — N petals rotated around a center disc, per-species tint via `MaterialPropertyBlock` on a single shared material so it batches. Ambient sway is per-petal `Sin(time + phase)`, no Animator; DOTween only for discrete events. Input is a physics raycast against petal colliders (the same path later serves touch and XR rays). Shaders stay simple/unlit — MaterialX-safe for PolySpatial and cheapest for the overlay.

### Performance budget (overlay mode)

<2% CPU, <60 MB RAM, `targetFrameRate = 30` dropping to 10 when idle, OnDemandRendering to skip redundant frames. Helpers are pooled; non-tended garden plants are pure math with no GameObjects.

### Save

Single versioned `SaveModel` → JSON in `Application.persistentDataPath`, two rotating slots, autosave every 30 s and on focus loss/quit, UTC timestamps. Offline progress is derived from the `lastSave` delta (capped, ~8 h).

## Conventions

- Namespace `CONFUSEDGAMEDEV.PollenGarden.<System>` — sub-namespaces `.Core` `.Flowers` `.Helpers` `.Garden` `.Economy` `.Platform` `.UI`
- C# parameters lowerCamelCase
- **No magic numbers.** All tuning lives in ScriptableObjects: `FlowerSpeciesData`, `HelperData`, `PowerupData`, `EconomyConfig`. Balance changes are asset edits, not code edits.
- Targeted edits over rewrites; verify with diffs
- Commit `.meta` files alongside every asset — a missing `.meta` silently breaks references for everyone else
- `Library/`, `Logs/`, `Temp/`, `UserSettings/`, `Build*/` are gitignored; `.csproj`/`.slnx` are regenerated by Unity
