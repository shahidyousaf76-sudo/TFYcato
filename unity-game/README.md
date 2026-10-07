# TFY Cat Adventure — Unity project

Unity **6 (6000.0 LTS)**, URP 17, Input System, uGUI, Newtonsoft JSON. Android first (IL2CPP, ARM64), iOS-ready.

## First open
1. Open this folder in Unity Hub. The packages in `Packages/manifest.json` install automatically.
2. **TFY ▸ 1. Setup Project**: URP assets and Quality levels (Low, Medium, High, Ultra), player settings, and `Assets/Scenes/Boot.unity`.
3. Press Play. The game runs fully offline with the bundled content (`Resources/default_content.json`).

## Firebase
Import the Firebase Unity SDK (Auth, Firestore, Functions, Remote Config, Analytics, Crashlytics), add `Assets/google-services.json`, then run **TFY ▸ 2. Enable Firebase**. That adds the `TFY_FIREBASE` define, which compiles `Backend/FirebaseBackend.cs`. Without the define, `OfflineBackend` is used and nothing Firebase-related is compiled.

## Code map (`Assets/Scripts`)
| Folder | Contents |
|---|---|
| `Domain/` (no UnityEngine) | Content models, `LevelPlanner` (fair, deterministic levels), `Rules` (stars, score, speed, usernames), `Progress` (C# port of the server's reward logic), `OfflineSync` (save state, pending runs, merge), `RuntimeConfig`. Tested by `DomainTests~` (`dotnet test`). |
| `Runtime/Core` | `App` (composition root: builds everything in code), `AppFlow` (startup flow and navigation), `MenuStage`, `GameEvents`, `Loc` (English + Danish) |
| `Runtime/Data` | `ContentService` (bundled → cached → online content, Remote Config), `SaveService`, `LocalFile` (checksum + backup), `Json` |
| `Runtime/Backend` | `IBackend`, `FirebaseBackend`, `OfflineBackend`, `BackendService` (boot, sync, shop, daily, boxes), `AnalyticsService`, `CrashReporting` |
| `Runtime/Gameplay` | `RunController`, `TrackBuilder` (streaming + pools), `PowerUps`, `TutorialDirector`, `InputController`, `CameraRig`; `Cat/` (`CatController`, `CatAnimator`, `CatRig`); `Obstacles/`; `Collectibles/` |
| `Runtime/World` | `EnvironmentBuilder` (40 m chunks, backdrop, ocean, clouds), `DecorFactory`, `SkyLighting` (6 time-of-day presets, fog, night lights) |
| `Runtime/Graphics` | `QualityService` (LOW/MEDIUM/HIGH/ULTRA + AUTO, adaptive), `PostFX` (ACES, bloom, grading, vignette, menu DOF), `MaterialLibrary`, `Shapes`, `Vfx` |
| `Runtime/Audio` | `Synth` (original generated sound), `AudioService` (music crossfades, ambience, pooled SFX, meow cooldown), `Haptics` |
| `Runtime/UI` | `UIKit` (code-built uGUI), `Icons` (SDF-drawn icons), `UIManager`, screens (menus, meta and play) |
| `Editor/` | `TFYSetup` (setup, Firebase define, level fairness report, Android build) |

## Replacing placeholder art and sound
Put files in `Assets/Resources` and they're picked up automatically:
- `Branding/tfy_logo.png` (sprite)
- `Audio/{id}`: e.g. `music_park`, `sfx_coin`, `meow_happy`
- `Icons/{name}`: `coin`, `star`, `heart`…
- `Decor/{type}` (prefab)
- `Fonts/TFYFont`

A rigged cat with an Animator (integer parameter `State`, float `RunSpeed`) can replace the procedural cat: `CatAnimator` drives it.
