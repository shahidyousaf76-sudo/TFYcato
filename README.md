# TFY Cat Adventure

A 3D cat-runner adventure for children aged about 5–12. **Android first, iOS-ready.** Unity (C#) game, Firebase backend, web admin dashboard, and a playable browser prototype.

```
TFY LOGO → GAME LOADING → CHECK EXISTING PLAYER
   new:      anonymous Firebase account → automatic username (e.g. Kitty4821) → profile   ┐
   existing: load player data (offline: local save)                                        ┴→ HOME → PLAY → RUN
```

No name, email, password, phone, address or sign-in is ever asked for. The child is playing within seconds, even offline.

| Folder | What it is |
|---|---|
| `unity-game/` | Unity 6 + URP game. Code-first: one component builds the whole game at runtime, and a one-click editor setup creates the project settings and Boot scene. |
| `firebase/` | Firestore rules and indexes, Cloud Functions (TypeScript), Remote Config template, and **the single source of game content** (`functions/src/content`). |
| `admin-dashboard/` | Next.js admin panel (static export for Firebase Hosting). Email accounts with an `admin` / `super_admin` role only. |
| `web-prototype/` | The same game in Three.js, playable in any browser, for testing game feel. Offline only: it saves in the browser. |
| `shared/` | Exported content JSON plus cross-language test fixtures. |

## Architecture

```
Unity game (C#)                        Admin dashboard (Next.js)
  │ Firebase SDK                          │ Firebase Web SDK
  ├─ Firebase Auth (anonymous) ◄──────────┤ Firebase Auth (email + role claim)
  ├─ Firestore: read own profile,         ├─ Firestore: read content, stats, audit
  │   write settings only                 │
  ├─ Cloud Functions ─────────────────────┴─ Cloud Functions (admin*, audited)
  │   createPlayer · completeLevel · syncOfflineRuns · claimDailyReward
  │   purchaseItem · openMysteryBox · updateLoadout · deleteMyData
  ├─ Remote Config (tuning, feature flags) ◄── published by the dashboard (mirrored to config/runtime)
  └─ Analytics (anonymous, no ads IDs) · Crashlytics
```

**Trust model.** Everything that gives value goes through Cloud Functions: coins, gems, stars, unlocks, progress, daily rewards, purchases and mystery boxes. The server validates each finished run against the level's exact totals and a minimum possible time. It computes the stars and score itself, then applies everything in one transaction. Each run has an idempotency key, so a retry never pays twice. Players may only change their own `settings`; the rules reject everything else.

**Offline-first.** The game writes every finished run to the local save before anything else, then shows a provisional result straight away. A C# copy of the server's reward logic computes it, and tests prove it gives the same numbers. When the device is back online, queued runs sync in order and the server's answer replaces the provisional one. Conflicts are safe by design: currency is decided only by the server, progress only ever goes up, and settings are last-writer-wins. Jumping and running never wait for the network.

## Quick start

### 1. Firebase (local, no account needed)
```bash
cd firebase/functions
npm install
npm test                 # 46 unit tests (rules, rewards, validation, content)
npm run test:emulator    # 21 end-to-end tests against the Auth/Firestore/Functions emulators (needs Java 11+)
npm run serve            # emulators + UI at http://localhost:4000
npm run seed:emulator    # (other terminal) load the default content
npm run export-content   # after editing src/content: regenerates Unity/web/admin copies + Remote Config template
```

### 2. Unity game
1. Open `unity-game/` in **Unity 6 (6000.0 LTS)**. Packages install from `Packages/manifest.json` (URP 17, Input System, uGUI, Newtonsoft JSON).
2. Run **TFY ▸ 1. Setup Project**. It creates URP assets and Quality levels for LOW, MEDIUM, HIGH and ULTRA (SSAO on HIGH and ULTRA), the Android settings (IL2CPP, ARM64, Vulkan+GLES3, ASTC, portrait) and the Boot scene.
3. Press **Play**. Without Firebase the game runs fully offline with the bundled content.
4. To connect Firebase:
   - Import the [Firebase Unity SDK](https://firebase.google.com/docs/unity/setup) packages: Auth, Firestore, Functions, Remote Config, Analytics, Crashlytics.
   - Put `google-services.json` in `Assets/` (and `GoogleService-Info.plist` for iOS).
   - Run **TFY ▸ 2. Enable Firebase**.
5. Run **TFY ▸ Build Android APK**, or build from File ▸ Build Profiles.

Controls: tap to jump, a second tap in the air for a double jump, swipe up for a long jump, swipe left/right to change lanes, swipe down to slide. In the editor: Space/W, E, A/D, S. On-screen buttons are optional (Settings).

### 3. Admin dashboard
```bash
cd admin-dashboard
cp .env.example .env.local        # Firebase web config; NEXT_PUBLIC_USE_EMULATORS=true for local
npm install && npm run dev        # http://localhost:3000
npm run build                     # static export → out/ (deploy: firebase deploy --only hosting:admin)
```
Create the first admin: add an email/password user in Firebase Authentication, then run
`npm run seed -- --make-super-admin you@example.com` in `firebase/functions`, using Application Default Credentials for the real project.

### 4. Web prototype
Open `web-prototype/index.html` in a browser (it works straight from disk).
Effects (`js/fx.js`):
- **HDR bloom:** Unreal bloom on a half-float target, then a single ACES tone-map and colour-grade pass. Brighter at night; off on LOW.
- **GPU particles:** sparkles, dust, confetti and fireworks; power-up auras and speed lines; shop trails; world weather (petals, leaves, snow, sea sparkles, night fireflies).
`web-prototype/tools/smoke-test.js` plays levels headlessly with an autopilot, and `tools/check-planner.js` checks that its levels match Unity's exactly.

## Deploying to your Firebase project
1. Create a Firebase project. Cloud Functions needs the **Blaze** plan.
2. Enable **Authentication**: Anonymous (players) and Email/Password (admins).
3. Create a **Firestore** database. Enable **Analytics** and **Crashlytics**.
4. Put the project id in `firebase/.firebaserc`, then run `firebase deploy --only firestore,functions,remoteconfig,hosting`.
5. Seed the content: either the Overview page in the dashboard (super admin), or `npm run seed`.
6. Before release, turn on **App Check** (Play Integrity / App Attest) for the functions.

## What's in the game

| Area | Implemented |
|---|---|
| Movement | Auto-run with smooth acceleration; 3 lanes; jump, double jump, long jump (automatic in the arrow zone before bridges), slide, fast-fall. Coyote time and input buffering make it forgiving. |
| Animations | Idle, walk, run, fast run, jump, double jump (flip), long jump, falling, landing, sliding, climbing (out of a gap), hit, victory, dancing, sleeping, happy. All procedural with smooth blending; a rigged model's Animator is supported. |
| Obstacles | Log, rock, branch (jump or slide), barrel, wall (double jump or go around), broken bridge (long jump), falling rocks, moving critter. Each is themed per world (e.g. the critter is a crab at the beach). |
| Warnings | Rocks shake, drop dust, play a rumble and show a landing shadow. Bridges show cracks, broken boards and glowing jump arrows. Critters walk a dotted path at a steady pace. Walls are tall with a glowing top. No surprise obstacles. |
| Fairness | The level planner never blocks all lanes unless all can be jumped, and always leaves ≥ 1 s of reaction time (tested even at the hardest Remote Config tuning). Coins lead into safe lanes and arc over jumps. |
| Levels | **100 levels**: 1–3 easy, 4–6 medium, 7–10 hard, 11–100 combinations that get harder step by step (a little more speed, length and density each level, top speed ≤ 16 m/s). Each runs start → easy → collectibles → obstacles → 🚩 → medium → (🚩) → special challenge → 🚩 → finish. Admins can add more. |
| Worlds | Green Park, Forest, City, Mountains, Beach, Snow World (snowmen, igloos, penguins, ice walls, falling snowballs), each with its own scenery, palette, fog, weather, music and ambience, plus hot air balloons drifting in every sky (their burner flames glow).
| Landmarks | 17 world-famous landmarks, one about every 200 m, with a name banner: Eiffel Tower, Big Ben, Statue of Liberty (City); Leaning Tower of Pisa, Taj Mahal, Dutch windmill (Park); pagoda, torii gate you run through, Stonehenge (Forest); Great Wall, Christ the Redeemer (Mountains); Sydney Opera House, Golden Gate Bridge you run under, The Little Mermaid (Beach); Mount Fuji, St. Basil's Cathedral, Santa Claus Village (Snow). Replace with final art at Resources/Landmarks/{id}. | Jungle, Desert, Fantasy, Candy, Sky Islands, Night City and Space are prepared as "coming soon". |
| Collectibles | Coins (guide the path), 3 big stars per level, fish, gems (once per gem), mystery boxes (opened on the server). |
| Stars | ⭐ finish · ⭐⭐ enough coins · ⭐⭐⭐ most coins + all 3 big stars. Stars are never required to progress. |
| Checkpoints | 2–3 flags per level. After running out of hearts: FROM FLAG (full hearts again), START OVER, or HOME. |
| Power-ups | Coin Magnet, Shield, Super Jump, Speed Boost, Double Coins, each with an effect on the cat and a HUD timer. Durations come from Remote Config. |
| Customisation | 9 cats, each with its own costume. Hats, crowns, glasses, shoes, backpacks, capes, scarves and trails. The Cat Shop has 7 categories with a 3D try-on preview and a confirmation step. Cosmetic only. |
| Rewards | 7-day daily treats (missing a day never resets the cycle), 10 achievements with rewards, mystery boxes. No real-money purchases. |
| Graphics | URP PBR materials, HDR emission and bloom, ACES tonemapping, soft shadows, SSAO (HIGH/ULTRA), realtime reflection probe (HIGH/ULTRA), fog, cloud layers, weather particles, day/night lighting (6 presets) with night lights, depth of field on menus only, anti-aliasing (FXAA/SMAA). LOW/MEDIUM/HIGH/ULTRA plus AUTO with adaptive step-down. |
| Audio | Music per world, ambience, sound effects and friendly meows with cooldowns. All synthesised originals; any sound can be replaced at `Resources/Audio/{id}`. |
| UI | Splash (TFY logo), loading screen with tips, Home ("Hi, Kitty4821!", big PLAY), Levels, Cat Shop, Achievements, Rewards, Settings, HUD, Pause, Level Complete, Fail, and an in-play tutorial with slow-motion prompts. English and Danish. |
| Settings | Music, sound, vibration, graphics quality, language, calm camera, on-screen buttons, how to play, and a parents' area behind a grown-up check (privacy notice, delete all data). |
| Errors | Friendly messages only ("Couldn't connect right now. You can keep playing offline."). Crash-safe saves with checksum and backup. Corrupted saves are recovered and reported (code only). |
| Admin | Overview (aggregate stats, per-level fail rates, where players are stuck), Levels (enable/disable, edit, create), Worlds/Shop/Achievements, Rewards & economy, Remote Config, Events & Announcements, Leaderboard review, Crashes & errors, Admins (roles), Audit log. |

## Child safety and privacy
- **No personal data is collected.** There is no name entry: usernames are generated from approved words plus digits, and the server rejects anything else. No chat, photos, birthdays or contact details.
- **Anonymous accounts.** Analytics consent sets ad storage, ad user data and ad personalisation to *denied*. The advertising ID and SSAID are turned off in the Android manifest.
- **Aggregate-only admin view.** Admins see aggregate statistics only; the rules don't let anyone browse children's profiles. Announcements to children can't contain links or contact details.
- **Leaderboards are off by default** (Remote Config). Even when on, they show generated names only.
- **Parents can delete data** in Settings ▸ For parents, after a grown-up check. This deletes the profile, the username, leaderboard entries and the account.
- Before publishing, review COPPA, GDPR-K/UK Children's Code, Google Play Families policy and Apple Kids Category for your markets, and add a privacy policy URL.

## Tests and verification

| What | Result |
|---|---|
| Cloud Functions unit tests (`npm test`) | 46 / 46 pass |
| Emulator tests: security rules + full player journey through the real functions (`npm run test:emulator`) | 21 / 21 pass |
| Unity game rules in C# (`dotnet test` in `unity-game/DomainTests~`) | 709 / 709 pass: fairness of all 100 levels, exact collectible counts, determinism, offline sync, and a parity test proving the C# rewards equal the server's over 250 random runs |
| Web ↔ Unity level layouts (`web-prototype/tools/check-planner.js`) | all 100 levels identical |
| All Unity runtime scripts compiled with the C# compiler, against Unity 2021.3 reference assemblies and uGUI (stand-ins for the URP, Input System and Firebase types used) | builds with and without `TFY_FIREBASE` |
| Admin dashboard typecheck + production build | passes |
| Web prototype headless smoke test (`tools/smoke-test.js`): every screen plus an autopilot playing levels 1, 5, 7 and 9 to the finish | all 4 completed, progress/coins/unlocks saved, no JS errors; screenshots in `web-prototype/tools/shots/` |
| Web prototype collision test (`tools/collision-test.js`): nobody playing level 8 | the cat bumps into obstacles and loses all 3 hearts; the Fail screen appears; FROM FLAG restarts at the last checkpoint with full hearts |

## Status and honest limits
- **The Unity project has not been opened in the Unity editor here** (no editor on this machine). The runtime scripts were compiled against Unity 2021.3 reference assemblies plus hand-written stand-ins for URP, the Input System and Firebase. Expect small fixes on first open, most likely in `Editor/TFYSetup.cs` (not compiled here) or where Unity 6 / URP 17 renamed something.
- **The Firebase Unity SDK calls** (`FirebaseBackend.cs`) were checked against stand-ins written from the SDK's public API, not against the real DLLs.
- **Art and audio are procedural placeholders**: the stylised cat, scenery and synthesised sounds are original and complete, but not final artist-made assets. You can drop in real models (`Resources/Decor/{type}`), sounds (`Resources/Audio/{id}`), icons (`Resources/Icons/{name}`) and a font (`Resources/Fonts/TFYFont`).
- **"Volumetric" fog and clouds are fog cards and cloud layers**, and reflections use reflection probes. URP has no built-in volumetric fog or screen-space reflections on mobile.
- **Logo:** the official TFY CATO logo (`shared/tfy-logo-original.png`) is used on the splash, loading and home screens, as the app icon (`unity-game/Assets/Branding/app_icon.png`, set by TFY ▸ 1. Setup Project) and in the admin dashboard. A transparent cut-out is at `unity-game/Assets/Resources/Branding/tfy_logo.png`.
- **The web prototype saves locally only**; the Firebase integration lives in the Unity game.
- **The brief was cut off at section 52** ("First-time tutorial … TAP TO JUMP"). The tutorial was built from the part that came through: interactive prompts, no text pages.
