# Pollen Garden — Development Plan

**Version:** 0.1 (July 2026)
**Engine:** Unity 6000.7· URP
**Root namespace:** `CONFUSEDGAMEDEV.PollenGarden`
**Lead platform:** macOS (primary dev environment) → Windows → Mobile → Vision Pro / MR

**One Unity project for all platforms.** Every target (macOS, Windows, iOS, Android, visionOS, Quest/PICO) is a build target of the same project. Platform differences are isolated to: the `IWindowPlatform` implementations, `#if UNITY_*` defines, per-platform native plugins under `Assets/Plugins/<platform>/`, and Unity Build Profiles. No forks, no per-platform branches.

---

## 1. Vision

An ambient idle game that lives on the desktop. A single flower floats over the workspace — background removed, click-through everywhere except the flower — and the player clicks petals to harvest pollen while they work. Helpers (bees, butterflies, hummingbirds) fly in autonomously. An Expand button opens the full game: shop, garden, progression.

**Pillars**

- **Ambient first** — never obstruct real work; click-through outside interactive bounds
- **Glanceable progress** — every look shows change (petal numbers, helpers, buffs)
- **Gentle economy** — steady growth, no punishing walls, no dark patterns
- **One codebase, many homes** — desktop overlay, mobile app, and MR widget share the same simulation core

**Platform phases**

| Phase | Platform | Presentation |
|---|---|---|
| 1 | **macOS (lead)** | Transparent overlay + expanded mode |
| 1 | Windows | Transparent overlay + expanded mode |
| 2 | iOS / Android | Standalone opaque app (expanded mode = the app) |
| 3 | Apple Vision Pro (PolySpatial) | Volumetric widget in Shared Space |
| 3 | Quest / PICO MR | Flower anchored to a real surface via passthrough |

---

## 2. Game Design Summary

### Core loop
Click petal → petal HP (displayed number) decreases → gain pollen → clear all petals → unlock next species + earn a seed.

### Currencies
| Currency | Earned by | Spent on |
|---|---|---|
| Pollen | Clicking petals; bee & butterfly visits | Trade → nectar (**100 pollen = 1 nectar**) |
| Nectar | Trading pollen; hummingbird visits | Helpers, powerups |
| Seeds | Completing a flower (1 each) | Planting garden plots |

### Helpers
| Helper | Cost | Interval | Petal dmg | Collects |
|---|---|---|---|---|
| Bee | 10 🍯 | ~6 s | 2 HP | Pollen (1 click's worth) |
| Butterfly | 10 🍯 | ~8 s | 2 HP | Pollen (1 click's worth) |
| Hummingbird | 50 🍯 | ~22 s | 4 HP | Nectar directly (+1) |

### Species curve (prototype values — retune from telemetry)
| # | Species | Petals | HP/petal | Pollen/click |
|---|---|---|---|---|
| 1 | Daisy | 6 | **100** | 5 |
| 2 | Poppy | 8 | 220 | 12 |
| 3 | Cornflower | 10 | 480 | 28 |
| 4 | Dahlia | 12 | 1,000 | 65 |
| 5 | Sunflower | 14 | 2,200 | 150 |

Pacing targets: first trade ~3 min, first bee ~10 min, first new species within a session.

### Garden & delegation
- Seeds → planted in plots → seedling with bloom timer → bloomed flower
- One flower **tended** (rendered on desktop) at a time; all plants persist petal state; switch freely
- Helpers can be **delegated per plant**; delegated crews work their plant even while another is tended (background plants simulate silently, no GameObjects). Unassigned helpers work the tended flower
- Completing a plant re-seeds that plot at the highest unlocked species, keeping its crew

### Powerups
| Powerup | Cost | Effect |
|---|---|---|
| Speedup | 30 🍯 | Helper intervals ×0.5 for 30 s |
| Golden Pollen | 30 🍯 | Pollen collection ×2 for 30 s |
| Instant Petal Cut | 500 🍯 | Destroys one full petal on the tended flower + pollen bonus |

Buff timers stack additively; countdown badges in overlay HUD.

### Offline progress (M3)
Elapsed time since last save → helper visits per plant (capped, e.g. 8 h) → welcome-back summary.

---

## 3. Technical Architecture

### Simulation core
Plain C#, zero platform/scene dependencies. Testable in EditMode, reused unchanged on every platform.

Sub-namespaces: `.Core` `.Flowers` `.Helpers` `.Garden` `.Economy` `.Platform` `.UI`

### System map
| System | Responsibility |
|---|---|
| GameManager | Boot, state machine, tick loop |
| EconomyManager | Balances, trade, purchase validation |
| GardenManager | Plots, plant states, bloom timers, tend switching |
| FlowerController / PetalController | Rendering + input for tended flower |
| HelperManager | Ownership, delegation ledger, visit scheduling, pooling |
| HelperAgent (+subtypes) | Fly-in → collect → fly-out FSM |
| PowerupManager | Buff timers, instant-cut |
| SaveManager | Versioned JSON, autosave, offline computation |
| UIManager | HUD, expanded menu, garden view, delegation sheet |
| WindowModeManager | Overlay ⇄ expanded; delegates to IWindowPlatform |

### Data-driven (ScriptableObjects — no magic numbers)
- `FlowerSpeciesData` — petal count, HP, pollen/click, colors, unlock order
- `HelperData` — cost, interval, damage, yield type, prefab
- `PowerupData` — cost, duration, effect
- `EconomyConfig` — trade rate, offline cap, pacing constants

### Flower construction
- **Flat stylized, unlit** (art direction decided — see §7): one *procedurally generated* petal mesh (~24 tris, 25 verts), assembled at runtime — N petals rotated around a center disc. Silhouette is a normalized Beta curve with two art-directable knobs (base roundness `p`, tip sharpness `q`); `q < 1` gives a blunt daisy tip, `q > 1` a sharp point. Mesh is authored base-at-origin so each petal's hinge is its own base — required for sway and droop
- **Per-species tint via a shared per-species material** (SRP-batched, GPU-Resident-Drawer eligible). *Not* `MaterialPropertyBlock`: MPB is precisely what breaks SRP Batcher compatibility and excludes renderers from the GPU Resident Drawer, which this project has enabled. Per-*petal* HP tint will need MPB and accepts that batching loss, confined to `PetalController`
- Geometry is single-sided; double-sidedness comes free from the material's `_Cull` float
- HP feedback: tint/alpha lerp + slight droop; TMP billboard number per petal; pooled particle pop on destruction
- Input: physics raycast against petal colliders (works for mouse, touch, and XR ray later); petal screen rects also feed click-through
- Ambient sway: per-petal `Sin(time + phase)` rotation, no Animator; DOTween only for events
- Shaders stay simple/unlit → MaterialX-safe for PolySpatial, best case for overlay perf

### The critical seam: `IWindowPlatform`
```
SetTransparent(bool)
SetClickThrough(bool)      // toggled per-frame from cursor vs interactive rects
SetAlwaysOnTop(bool)
SetWindowRect(...)
SetExpanded(bool)
```

| Implementation | Approach |
|---|---|
| **MacWindowPlatform (lead)** | Swift/Obj-C `.bundle` plugin. `NSWindow.isOpaque=false`, clear `backgroundColor`, `ignoresMouseEvents` toggling, floating level, `CollectionBehavior.canJoinAllSpaces`. `CAMetalLayer.isOpaque=false`; camera clears to alpha 0 |
| WindowsWindowPlatform | P/Invoke user32/dwmapi: `DwmExtendFrameIntoClientArea`, `WS_EX_LAYERED`, dynamic `WS_EX_TRANSPARENT`, `HWND_TOPMOST` |
| OpaquePlatform | No-op for mobile/MR |

**Renderer warning:** URP post-FX can destroy the alpha channel → minimal post-FX + final alpha-preserving blit. Validate in M0 before any feature work.

*Concrete starting state, verified in `Assets/Settings/`:* `PC_RPAsset.asset` has `m_AllowPostProcessAlphaOutput: 0` and `m_PrefilterAlphaOutput: 0` — the second strips the alpha-output shader variants from builds, so flipping only the first will work in the Editor and fail in a player. The scene camera has `m_RenderPostProcessing: 1` and clears to Skybox. Also active: SSAO (`Source: DepthNormals`, forcing a full prepass over an unlit scene) and Forward+.

**AA and alpha are one decision, not two.** MSAA is currently *disabled* (`m_MSAA: 1` == `MsaaQuality.Disabled`), and flat-stylized art is all silhouette, so aliasing is the biggest visual-quality risk to the chosen art direction. The usual fix — post-process FXAA/SMAA — is exactly what endangers window alpha. Resolve both together in M0.

### Click-through strategy
Per frame in overlay mode: UI layer sends union of interactive screen rects (petals, HUD, Expand) to platform layer. Cursor inside any rect → click-through off (Unity gets input); outside → on (desktop gets input). Few-pixel hysteresis to avoid edge flicker.

### Save system
- Single versioned `SaveModel`, JSON, two rotating slots
- Autosave every 30 s + on focus loss/quit; `Application.persistentDataPath`
- UTC timestamps; offline progress from lastSave delta

### Performance budget (overlay)
- <2% CPU on a modern laptop, <60 MB RAM
- `targetFrameRate = 30`, drop to 10 when idle; OnDemandRendering to skip redundant frames
- Helpers pooled; background plants are pure math

---

## 4. Platform Plans

### macOS — LEAD
- Swift/Obj-C `.bundle` exposing a C API for window control — a standard Unity native plugin living at `Assets/Plugins/macOS/` inside the project (compiled once, committed or built in CI); Unity C# calls it via `[DllImport]`. Not a separate app.
- Risks: Metal alpha handling, Spaces/Mission Control behavior, full-screen app interaction, notarization + hardened runtime in CI
- Distribution: notarized DMG direct download first; Mac App Store later **only if** sandbox permits overlay styling (verify — direct is the safe path)

### Windows
- Pure P/Invoke (no compiled plugin → simpler CI)
- Risks: multi-monitor DPI; fullscreen-exclusive apps render above overlay (accepted)
- Distribution: direct and/or Steam

### Mobile (Phase 2)
- Expanded mode = whole app; overlay code compiled out via platform defines
- Opt-in notifications for bloom completion

### Vision Pro & MR (Phase 3)
- visionOS via PolySpatial: bounded volume in Shared Space (requires Unity Pro; shaders must survive MaterialX)
- Quest/PICO: passthrough + plane detection anchor; hand-ray/poke input

---

## 5. Milestones

> M0 spikes the transparent window on **macOS first, then Windows**, before any feature work — it's the project's only novel technical risk.

- [ ] **M0 — Platform spikes**
  - [x] macOS: bundle plugin — transparent NSWindow, click-through toggle, always-on-top *(Obj-C, `Assets/00.Plugins/macOS/`, source in `Source~/`; borderless + floating level verified via window server on a real player build)*
  - [x] macOS: URP alpha-preserving output validated (flower visible over desktop) — flip `m_AllowPostProcessAlphaOutput` **and** `m_PrefilterAlphaOutput` on `PC_RPAsset`, camera clear → Solid Color alpha 0, verify in a *player* build not just the Editor *(verified in player build over the desktop, Aug 2026)*
  - [ ] Decide AA together with alpha (MSAA vs post-process AA vs none); re-evaluate whether SSAO earns its prepass on an unlit scene
  - [x] macOS: per-frame click-through toggling from screen rects *(`InteractiveScreenRects` + `ClickThroughManager` + `PG_TryGetCursorPixels` — the OS is asked for the cursor because a click-through window stops receiving mouse events; verified in player: petals clickable, desktop clicks pass through elsewhere)*
  - [ ] Windows: same proof via P/Invoke
  - [x] `IWindowPlatform` interface + all three implementations stubbed *(`Assets/01.Scripts/Platform/`: Mac real, Windows stub, Opaque no-op, plus `WindowModeManager`)*
  - **Exit:** flower quad over desktop, clicks pass through outside it, on both OSes

- [ ] **M1 — Core loop** (build on Mac; verify opaque build on Windows)
  - [x] Procedural flower assembly from `FlowerSpeciesData` (Daisy + Poppy) — *geometry + radial assembly + per-species tint + per-petal HP done; species assets exist for Daisy, Poppy, Cornflower, Dahlia (`Assets/05.Data/Flowers/`, 20 EditMode tests green)*
  - [ ] Petal clicking, HP display, pollen/nectar, 100:1 trade — *clicking + HP labels + petal/flower destruction + species progression done (`PetalClickInput`, `FlowerProgression`); pollen/nectar economy and trade still pending. Daisy temporarily at 10 HP/petal for testability (design value: 100)*
  - [ ] Save/load with versioning + autosave
  - [ ] Overlay HUD + expand/collapse
  - **Exit:** playable overlay loop on Mac; same build runs on Windows

- [ ] **M2 — Helpers**
  - [ ] HelperAgent FSM (fly-in / collect / fly-out), pooling
  - [ ] Bee, Butterfly, Hummingbird + shop
  - [ ] Speedup & Golden Pollen buffs, Instant Petal Cut
  - **Exit:** idle progression works while focused; autosave solid

- [ ] **M3 — Garden**
  - [ ] Seeds, plots, bloom timers
  - [ ] Per-plant delegation + background simulation
  - [ ] Tend switching; offline progress + welcome-back summary
  - **Exit:** full prototype feature parity + offline gains

- [ ] **M4 — Polish & beta**
  - [ ] Juice pass (particles, audio, haptics), settings
  - [ ] Performance pass vs. budget
  - [ ] macOS notarization pipeline + DMG; Windows installer
  - [ ] Telemetry (time-to-first-bee, session cadence)
  - **Exit:** closed beta on both desktop platforms

- [ ] **M5 — Mobile port**
  - [ ] Opaque app build, touch polish, notifications
  - **Exit:** iOS/Android beta

---

## 6. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| URP post-FX destroys window alpha | Overlay unusable | M0 spike; custom final blit; minimal post-FX |
| Mac App Store sandbox blocks overlay styling | Channel lost | Direct notarized distribution first |
| OS updates change window APIs | Breakage in the wild | Thin isolated platform layer; per-OS integration tests |
| 100 HP start feels grindy | Early retention | Telemetry on time-to-first-bee; tune pollen/click, not HP |
| PolySpatial shader constraints | Art rework later | MaterialX-safe shaders from day one |

## 7. Open questions

- Monetization: premium vs. free + cosmetic species packs
- Overlay audio: ship at all?
- Windows channel: Steam vs. itch.io
- Launch localization: EN/JA/ES as the first set?
- ~~Art direction: flat stylized vs. soft 3D — decide before modeling the petal mesh~~ — **DECIDED: flat stylized, unlit, zero lights.** Best overlay perf and the safest MaterialX/PolySpatial target. The mesh generator keeps `p`/`q` silhouette knobs, so species can still differ in shape without changing the look

## Conventions

- Namespace: `CONFUSEDGAMEDEV.PollenGarden.<System>`
- C# parameters: lowerCamelCase
- No magic numbers — all tuning in ScriptableObjects
- Targeted edits over rewrites; verify with diffs