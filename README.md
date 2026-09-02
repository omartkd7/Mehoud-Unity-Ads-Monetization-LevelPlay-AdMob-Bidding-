# 📱 METHOUD — Unity LevelPlay Ads Monetization System

A **plug-and-play ad monetization system** for Unity mobile games, built on **Unity LevelPlay (ironSource)** with **AdMob bidding**.
Drop in two scripts, fill in your keys, wire a button — and you have Interstitial, Rewarded, Banner, and App Open ads with retention guards, frequency caps, GDPR/CCPA consent, and retry logic already handled.

> Built for **Android + iOS**. Compiles with or without the SDK installed (guarded by `#if UNITY_LEVELPLAY`).

---

## 📑 Table of Contents

1. [How it works (the big picture)](#-how-it-works-the-big-picture)
2. [The files](#-the-files)
3. [Ad lifecycle — load then show](#-ad-lifecycle--load-then-show)
4. [Installation (step by step)](#-installation-step-by-step)
5. [How to show each ad type](#-how-to-show-each-ad-type)
6. [The retention guards (why an ad may NOT show)](#-the-retention-guards-why-an-ad-may-not-show)
7. [Instant / test mode](#-instant--test-mode)
8. [Inspector settings reference](#-inspector-settings-reference)
9. [Firebase Analytics (optional)](#-firebase-analytics-optional)
10. [Before you publish ⚠️](#-before-you-publish-️)
11. [Troubleshooting](#-troubleshooting)

---

## 🧠 How it works (the big picture)

The flow is always the same:

```
[ UI Button / Game event ]
        │
        ▼
   LevelManager.cs      ← game side: your buttons call this
        │
        ▼
    AdManager.cs        ← the brain: connects to SDK, loads & shows ads, decides WHEN
        │
        ▼
  LevelPlay SDK (ironSource + AdMob bidding)
        │
        ▼
     🎬  Ad shows on screen
```

- **`AdManager`** is a **singleton** (`AdManager.Instance`) that lives for the whole game (`DontDestroyOnLoad`). You never create more than one.
- Ads are **pre-loaded in the background** the moment the SDK is ready, so they show **instantly** when you ask.
- After every ad closes, the **next** one auto-loads — no manual reloading needed.

---

## 📂 The files

| File | Role |
|------|------|
| `file methoud/AdManager.cs` | **The brain.** SDK init, loading/showing all 4 ad types, retention guards, consent, stats, retry logic. |
| `file methoud/LevelManager.cs` | **The game side.** Thin call sites your buttons/game events hook into. |
| `file methoud/RewardedPromptUI.cs` | Optional custom "Watch / Decline" overlay shown before a rewarded ad. |
| `file ferbise anltyec/FirebaseAnalyticsManager.cs` | Optional Firebase analytics logging. |
| `file ferbise anltyec/FirebaseAdsBridge.cs` | Optional bridge that forwards ad events to Firebase. |
| `file methoud/last docm/*.html` | Offline API reference documentation. |

---

## 🔄 Ad lifecycle — load then show

**The #1 thing to understand:** an ad must be **loaded** before it can **show**.

```
App start
   │
   ▼
AdManager.Start()  →  InitializeSDK()          // connect using your App Key
   │
   ▼
HandleSDKInitSuccess()                          // SDK ready ✅
   │  ├─ LoadRewarded()
   │  ├─ LoadInterstitial()
   │  ├─ LoadBanner()
   │  └─ LoadAppOpen()
   ▼
Ad is loaded & waiting in memory  ✅
   │
   ▼
You call ShowInterstitialNow() / ShowRewarded() / ...
   │
   ▼
🎬 Ad shows
   │
   ▼
On close → auto Load next ad → ready again
```

👉 If you try to show an ad **before it finishes loading**, nothing happens and the console logs `no ad loaded yet, loading…`. Wait ~2–3 seconds after launch, then it's ready.

---

## 🛠 Installation (step by step)

### 1. Install the LevelPlay SDK
`Window → Package Manager → Unity Registry → search "LevelPlay" → Install`
(package id: `com.unity.services.levelplay`). The `UNITY_LEVELPLAY` define is added automatically and the scripts activate.

### 2. Add the scripts to your project
Copy the `file methoud/` scripts into your Unity project's `Assets/` folder.

### 3. Create the AdManager GameObject
- Create an empty GameObject in your **first scene** → name it `AdManager`
- Drag `AdManager.cs` onto it
- It survives every scene automatically (`DontDestroyOnLoad`), so add it only once.

### 4. Fill in your keys (Inspector)
Select the `AdManager` GameObject and fill the fields from your **LevelPlay dashboard**:

| Field | Example |
|-------|---------|
| App Key (Android) | `2405ac19d` |
| App Key (iOS) | `YOUR_IOS_APP_KEY` |
| Rewarded Ad Unit ID | `s4e1a6c1c6fb914b` |
| Interstitial Ad Unit ID | `rm6p46jcmhtdvcfm` |
| Banner Ad Unit ID | `8qvzekq8vg28qli0` |
| App Open Ad Unit ID | `YOUR_APP_OPEN_UNIT_ID` |

### 5. Wire a button
- Select your Button → **Inspector → Button → OnClick() → `+`**
- Drag the GameObject that has `LevelManager` into the slot
- Choose from the dropdown:
  - `LevelManager → OnButtonShowInterstitial` → interstitial
  - `LevelManager → OnButtonShowRewarded` → rewarded

### 6. Press Play
Wait ~2–3 seconds (SDK loads the first ad), click your button → ad shows. In the Editor you'll see **LevelPlay test ads** — that's expected.

---

## 🎬 How to show each ad type

### Interstitial (full-screen)
```csharp
// Respects all retention guards (recommended for release):
AdManager.Instance.TryShowInterstitial(AdManager.Placement.LevelComplete);

// Force it NOW, ignoring every guard (test buttons only):
AdManager.Instance.ShowInterstitialNow();
```

### Rewarded (watch → get reward)
```csharp
// Direct — no prompt overlay:
AdManager.Instance.ShowRewarded(AdManager.Placement.DoubleCoins, onRewarded: () =>
{
    coins *= 2;   // ← give the reward here
});

// With a custom Watch/Decline overlay first (if RewardedPromptUI is in the scene):
AdManager.Instance.ShowRewardedPrompt(
    AdManager.Placement.ContinueGame,
    onAccepted: RevivePlayer,
    onDeclined: GoToMenu);
```

### Banner
```csharp
AdManager.Instance.ShowBanner();    // show
AdManager.Instance.HideBanner();    // hide (e.g. during gameplay)
AdManager.Instance.DestroyBanner(); // remove entirely
```

### App Open
Shown automatically when the player returns to the app (after session 1), gated by `appOpenCooldownSeconds`. No manual call needed.

### Ready-made game hooks
`AdManager` already exposes convenient hooks:
```csharp
AdManager.Instance.OnLevelComplete(levelNumber);          // interstitial on level end
AdManager.Instance.OnGameOver(onRevived, onDied);         // "watch to continue"
AdManager.Instance.OnDoubleRewardOffer(label, onDoubled); // "watch to double"
AdManager.Instance.OnDailyBonusOffer(onClaimed);          // daily bonus
```

---

## 🛡 The retention guards (why an ad may NOT show)

`TryShowInterstitial()` checks these **before** showing. **ALL** must pass, or no ad appears. This is intentional — it protects revenue and player retention:

| Guard | Rule | Default |
|-------|------|---------|
| Ad-free | Player bought "remove ads" (`IsAdFree()`) | — |
| **Day gate** | App installed at least N days | `interstitialUnlockAfterDays = 1` |
| **Tutorial gate** | Player finished N levels | `tutorialLevelCount = 3` |
| **Session cap** | Max interstitials per app session | `maxInterstitialsPerSession = 6` |
| **Cooldown** | Min seconds between interstitials (per country tier) | `120 / 90 / 60` |

> Rewarded ads have **no** cooldown/day gate — they only require an ad to be loaded, because the player opts in.

---

## ⚡ Instant / test mode

While testing you want ads to fire on **every click** with no waiting. Two ways:

1. **`instantShowNoLimits = true`** (Inspector) → `TryShowInterstitial()` skips ALL four guards.
2. **`ShowInterstitialNow()` / `ShowRewardedNow()`** → always bypass the guards, regardless of the toggle. Meant for test buttons.

```csharp
if (!instantShowNoLimits)
{
    // day / level / session / cooldown checks run here
}
// otherwise → show immediately
```

---

## 🎛 Inspector settings reference

| Setting | What it does | Test value | Release value |
|---------|--------------|-----------|---------------|
| `instantShowNoLimits` | Skip all interstitial guards | `true` | **`false`** |
| `cooldownTier1/2/3` | Seconds between interstitials by country tier | `0 / 0 / 0` | **`120 / 90 / 60`** |
| `interstitialUnlockAfterDays` | Days before interstitials start | `0` | **`1`** |
| `tutorialLevelCount` | Levels before interstitials start | `0` | **`3`** |
| `maxInterstitialsPerSession` | Cap per app session | `9999` | **`6`** |
| `appOpenCooldownSeconds` | Cooldown for App Open ads | `180` | `180` |
| `enableTestMode` | Force LevelPlay test ads (auto-on in Editor) | on | **off** |

---

## 📊 Firebase Analytics (optional)

The `file ferbise anltyec/` folder adds **Firebase Analytics v13.7.0** on top of the ad system so you can see, in the Firebase console, how your ads and levels actually perform. It's **fully optional** — the ad system works without it.

### The two files

| File | Role |
|------|------|
| `FirebaseAnalyticsManager.cs` | **The logger.** A singleton (`FirebaseAnalyticsManager.Instance`) that initializes Firebase, identifies the device, and logs game + ad events. |
| `FirebaseAdsBridge.cs` | **The auto-connector.** Listens to `AdManager.OnAdsCompleted` and forwards every finished ad to Firebase automatically — you write zero extra code. |

### How the bridge works

```
AdManager  ──(OnAdsCompleted event)──►  FirebaseAdsBridge  ──►  FirebaseAnalyticsManager  ──►  Firebase console
```

Whenever any ad finishes (rewarded earned/skipped, interstitial closed, banner loaded, app open closed), `AdManager` fires `OnAdsCompleted`. `FirebaseAdsBridge` catches it and calls `LogAdCompleted(adType, placement, rewarded)`. So **every ad is tracked automatically** once the bridge is in the scene.

### Setup

1. Install the **Firebase Analytics** SDK for Unity (import `google-services.json` for Android / `GoogleService-Info.plist` for iOS from your Firebase project).
2. Create a GameObject → add `FirebaseAnalyticsManager.cs` (it's a `DontDestroyOnLoad` singleton — add once).
3. Add `FirebaseAdsBridge.cs` to the **same GameObject as `AdManager`**. It auto-subscribes on enable.
4. Done — ad events now flow to Firebase with no extra calls.

### Events it logs

**Ads:** `ad_completed` (with `ad_type`, `placement`, `rewarded`), `revive_offer_shown`, `revive_accepted`, `revive_declined`
**Game flow:** `session_start`, `level_start`, `level_end`, `level_fail`, `level_selected`, `play_pressed`, `tutorial_complete`
**Navigation:** `replay`, `go_home`, `continue_to_next`, `screen_view` (main_menu), `app_open`

### Logging your own events manually
```csharp
FirebaseAnalyticsManager.Instance.LogLevelStart(levelNumber);
FirebaseAnalyticsManager.Instance.LogReviveAccepted(levelNumber);

// Generic helpers:
FirebaseAnalyticsManager.Instance.LogEvent("custom_event");
FirebaseAnalyticsManager.Instance.LogEvent("shop_open", "coins", 250);
```

> The manager guards every call with an `IsReady()` check, so events before Firebase finishes initializing are skipped safely (logged to console, never crash).

---

## ⚠️ Before you publish

> **Publishing in instant/test mode WILL get your AdMob / LevelPlay account banned for invalid traffic.**

Reset these in the Inspector before your release build:

| Field | Set to |
|-------|--------|
| `instantShowNoLimits` | **`false`** |
| `cooldownTier1 / 2 / 3` | **`120 / 90 / 60`** |
| `interstitialUnlockAfterDays` | **`1`** |
| `tutorialLevelCount` | **`3`** |
| `maxInterstitialsPerSession` | **`6`** |
| `enableTestMode` | **`false`** |

Also: use `TryShowInterstitial()` (respects guards) in the shipped game — keep `ShowInterstitialNow()` for test builds only. And handle **GDPR/CCPA consent** on first launch:
```csharp
AdManager.SetUserConsent(true);   // after the user accepts
AdManager.SetDoNotSell(false);    // CCPA
```

---

## 🧩 Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `no ad loaded yet, loading…` | Clicked before the SDK loaded an ad | Wait 2–3 s after launch, click again |
| No ad ever shows (interstitial) | A retention guard is blocking it | Set `instantShowNoLimits = true` to test, or check day/level/session/cooldown |
| Test ads in Editor | `enableTestMode` auto-on in Editor/dev builds | Normal — real ads only on device release builds |
| `App Key not set` error | App Key empty or still `YOUR_...` | Fill it from the LevelPlay dashboard |
| SDK init failed | Network / wrong key | Auto-retries up to 3× (30 s backoff); verify key & connection |
| Nothing compiles / all ads dead | SDK not installed | Package Manager → install `com.unity.services.levelplay` |

### ⚠️ "Only Unity Ads / ironSource fills — AdMob never shows"

**Important: `AdManager.cs` never talks to AdMob directly.** It only calls the LevelPlay
SDK — AdMob is a *bidder* that LevelPlay auctions internally. So this symptom is **never
a bug in this script**; it's always a missing step in the Unity/LevelPlay/AdMob
dashboards. Check these in order:

1. **AdMob adapter installed?** `Window → LevelPlay → Integration Manager` → the AdMob
   row must say **Installed** (not just "Available"). Without the adapter binary, AdMob
   can't be called at all, no matter what's configured online — every impression falls
   back to ironSource/Unity Ads.
2. **AdMob activated in LevelPlay?** `LevelPlay dashboard → Monetize → SDK Networks →
   AdMob` → must be **Activated**, linked via "Sign in with Google" (or a pasted
   `pub-XXXXXXXXXXXXXXXX` Publisher ID as fallback).
3. **Bidding instance added per ad unit?** For **each** ad unit (Interstitial, Rewarded,
   Banner) → Mediation Group → Add Instance → AdMob → mode must be **"In-App Bidding"**,
   not "Waterfall" — and the AdMob Ad Unit ID pasted there must exactly match the one
   from `apps.admob.com` for that same format.
4. **AdMob app approved?** `apps.admob.com → Apps → your app` → status column must NOT
   say "Pending review". A pending/unapproved AdMob app returns **zero bids**, so
   ironSource (which needs no such approval) always wins by default — this looks
   exactly like "only Unity Ads works."
5. **AndroidManifest App ID present and real?** Missing or placeholder
   `com.google.android.gms.ads.APPLICATION_ID` meta-data crashes the AdMob adapter
   silently (or the whole app on launch). See `manifest_setup.md`-style setup in your
   Unity project (`ca-app-pub-XXXX~YYYY`, real value, not `YOUR_...`).
6. **iOS `Info.plist` configured?** `GADApplicationIdentifier`, `SKAdNetworkItems`, and
   `NSUserTrackingUsageDescription` must be present, and ATT must be requested **before**
   `LevelPlay.Init()`. No ATT/IDFA on iOS can drop AdMob bids to near-zero even when
   everything else is right.
7. **Verify with the test suite, on a real device:** call `LevelPlay.LaunchTestSuite()`
   (never trust the Editor — it never returns real fills). If AdMob doesn't even *appear*
   as a bidder there, the problem is steps 1–4 above, not code.
8. **Check win rate, not just presence:** `LevelPlay dashboard → Monetize → Reports →
   Mediation`, filter by AdMob, wait 24h of live traffic. AdMob *appearing but losing*
   every auction to Unity Ads is normal bidding behavior (highest bid wins) — that's not
   a bug either, it just means AdMob's bid is currently lower in your market/region.

### ⚠️ "IronSource network shows 0 fill in Mediation Report — only Unity Ads fills"

Different symptom, same root cause pattern. **`IronSource` is the network built into the
LevelPlay SDK core itself** (no separate adapter to install, unlike AdMob/AppLovin/Meta),
so when it shows 0 impressions while `Unity Ads` fills fine, it's almost always one of:

1. **IronSource demand not activated/approved for this app yet.**
   `LevelPlay dashboard → Monetize → SDK Networks → IronSource` — new apps commonly sit
   in a review/learning phase before ironSource's own demand starts bidding. This is
   expected, not a bug.
2. **No IronSource instance added to the Mediation Group for that ad unit.** Check the
   waterfall/bidding group per ad unit (Interstitial/Rewarded/Banner) — the instance must
   be present and enabled, exactly like the AdMob instance in the section above.
3. **Low fill in your test country.** IronSource fill density varies a lot by country —
   Tier 2/3 countries can show near-0% while Unity Ads still has broad coverage there.
4. **Traffic volume too low.** Some networks need a minimum daily impression volume
   before they compete meaningfully in the auction; a low-traffic new app may simply not
   see IronSource bid yet.
5. **Confirm in `LaunchTestSuite()` on a real device** — if IronSource doesn't appear as
   a bidder there either, the fix is activation/account status (points 1–2), not code.

---

## 🤖 Full Prompt — reuse this on any other Unity project

Copy everything in the box below into Claude Code (or any AI coding agent with Unity
MCP / file access) at the root of a **different** Unity project to have it run the exact
same audit-and-fix pass documented in this repo. It's written from real findings —
several of these steps exist because they were *actually broken* in a real project, not
theoretical.

````
You are integrating Unity LevelPlay (ironSource) ads + AdMob bidding + Firebase into
this Unity project, using the METHOUD system (AdManager.cs / LevelManager.cs /
FirebaseAnalyticsManager.cs / FirebaseAdsBridge.cs). Don't just write code — verify each
step actually took effect. Work through this in order:

1. AUDIT FIRST
   - Find whether AdManager.cs / FirebaseAnalyticsManager.cs already exist somewhere in
     Assets/ (they're often dropped into the project but never actually placed on a
     GameObject in any scene — check for that specifically: search every .unity scene
     file for the script's GUID, not just for the .cs file's existence).
   - Check Packages/manifest.json for `com.unity.services.levelplay` and Firebase
     packages — confirm they're actually resolved in Library/PackageCache, not just
     listed.
   - Check the Unity Editor console for existing errors before touching anything.

2. WIRE THE SINGLETON INTO THE FIRST SCENE
   - Find the scene at Build Settings index 0.
   - Create one GameObject (e.g. "___AdsAndAnalytics") there, add AdManager +
     FirebaseAnalyticsManager + FirebaseAdsBridge. It must be the ONLY instance —
     AdManager.Awake() calls DontDestroyOnLoad, so it persists into every other scene
     automatically. Do not add another copy anywhere else.
   - Fill in the real App Key, and Banner/Interstitial/Rewarded Ad Unit IDs from the
     user's LevelPlay dashboard, and the AdMob App ID from apps.admob.com. Never invent
     placeholder-looking values — ask for the real ones.

3. ⚠️ CHECK `UNITY_LEVELPLAY` IS ACTUALLY DEFINED — do not assume it auto-defines.
   - The real LevelPlay SDK's .asmdef (Library/PackageCache/com.unity.services.levelplay@*/
     Runtime/Unity.LevelPlay.asmdef) has an EMPTY `versionDefines` array. It does NOT
     auto-add `UNITY_LEVELPLAY` the way some AdManager.cs templates assume.
   - Check ProjectSettings/ProjectSettings.asset → scriptingDefineSymbols. If
     `UNITY_LEVELPLAY` isn't listed for Android/iPhone/Standalone, add it manually — until
     you do, AdManager silently runs its `#else` stub branch and logs
     "[AdManager] LevelPlay SDK NOT installed!" even though the package is installed.
     This is the single most common reason "I wrote the ad code but nothing shows."

4. ⚠️ ANDROID MANIFEST — "Custom Main Manifest" REPLACES, it does not merge.
   - If Assets/Plugins/Android/AndroidManifest.xml doesn't exist, you need it for the
     AdMob App ID meta-data (`com.google.android.gms.ads.APPLICATION_ID`) — without it,
     the app crashes on launch once ads go live.
   - BUT: enabling "Custom Main Manifest" (ProjectSettings → useCustomMainManifest: 1)
     makes Unity use YOUR file AS-IS instead of its own generated one. If your file only
     contains `<application><meta-data>...</meta-data></application>`, you have SILENTLY
     DELETED the `<activity>` block with the MAIN/LAUNCHER intent-filter that Unity
     normally generates — the APK builds successfully with NO errors, installs fine, but
     has NO launcher activity, so no icon ever appears on the device and the app cannot
     be opened. This is invisible until you inspect a real built APK.
   - Your custom manifest MUST include (adjust theme/attrs to the project's Unity
     version):
     ```xml
     <activity android:name="com.unity3d.player.UnityPlayerActivity"
               android:theme="@style/UnityThemeSelector"
               android:launchMode="singleTask"
               android:configChanges="mcc|mnc|locale|touchscreen|keyboard|keyboardHidden|navigation|orientation|screenLayout|uiMode|screenSize|smallestScreenSize|fontScale|layoutDirection|density"
               android:exported="true">
       <intent-filter>
         <action android:name="android.intent.action.MAIN"/>
         <category android:name="android.intent.category.LAUNCHER"/>
       </intent-filter>
       <meta-data android:name="unityplayer.UnityActivity" android:value="true"/>
     </activity>
     ```
   - VERIFY, don't assume: after any build, run
     `aapt2 dump badging path/to.apk | grep launchable-activity` — if that line is
     missing, the app has no entry point, full stop.

5. SET PRODUCTION-SAFE DEFAULTS, EXPLAIN THE TRADE-OFF
   - Many AdManager.cs templates ship with `instantShowNoLimits = true` and 0-second
     cooldowns as the DEFAULT (meant for the author's own testing) — if left as-is in a
     shipped build, interstitials fire with zero cap, which is grounds for a Play
     Store/App Store policy strike for disruptive ads. Set `instantShowNoLimits = false`,
     real cooldowns (45–90s by tier), a session cap, and a tutorial-level guard — then
     tell the user explicitly they can flip `instantShowNoLimits = true` temporarily for
     their own testing, but must set it back before release.
   - To change a serialized default and have it actually apply to an already-placed
     component: edit the .cs default, trigger AssetDatabase refresh, wait for
     recompile, then remove+re-add the component (or delete/recreate the GameObject) —
     editing the .cs alone does NOT retroactively change values already serialized onto
     an existing component instance in a scene.

6. WIRE ADS INTO REAL GAMEPLAY CODE, NOT JUST NEW UNUSED METHODS
   - Grep the project's actual gameplay scripts (not just the ad-system folder) for
     commented-out or dead references to a PREVIOUS ad SDK (e.g. `Advertisements.Instance`,
     `IronSource.Agent`, old ad manager classes). These mark the exact spots — retry
     buttons, level-complete, menu transitions — where ad calls belong. Replace the dead
     code with real `AdManager.Instance.TryShowInterstitial(...)` / `ShowBanner()` calls.
   - Then VERIFY the wiring is real: grep the actual .unity scene files (not just the
     .cs) for the method name inside `m_TargetAssemblyTypeName` / `m_MethodName` — a
     button's OnClick binding in the Inspector is separate from the method existing in
     code. A method can exist and compile fine while zero buttons call it.
   - Check for buttons already wired to ad-sounding methods on scripts that no longer
     exist in the project (`grep -rl "SomeOldAdsManager" Assets --include="*.unity"` but
     the .cs file is gone) — these are silently broken leftovers from a previous
     integration attempt. Don't assume every ad-related button reference is yours to fix;
     check whether it's even reachable by the player (e.g. leftover template UI like an
     unused multiplayer panel) before spending effort repairing it.

7. FIREBASE — VERIFY THE PACKAGE-NAME MATCH AND THE NATIVE LIBS
   - `Assets/google-services.json` → `package_name` must match the project's
     applicationIdentifier EXACTLY, including case. Android package names are
     case-sensitive; Firebase's match is case-sensitive too. If the user already
     registered the app in Firebase/Play Console with a specific case, match it — don't
     "fix" the casing yourself just because it looks unconventional.
   - `DllNotFoundException: FirebaseCppApp-...` in the Unity Editor Play Mode is EXPECTED
     and harmless — Firebase's native library only loads in a real build. Verify the real
     device path works by unzipping the actual Android .aar (usually under
     `Assets/GeneratedLocalRepo/Firebase/.../*.aar`) and confirming it contains
     `jni/arm64-v8a/`, `jni/armeabi-v7a/` .so files — don't just trust that it's fine.

8. VERIFY THE ADMOB BIDDING ADAPTER, NOT JUST THE CORE SDK
   - `Assets/LevelPlay/Editor/ISAdMobAdapterDependencies.xml` (or similarly-named files
     for other networks) confirms the AdMob adapter + Google Mobile Ads SDK dependency is
     registered — this is separate from the core LevelPlay package and easy to miss.
   - This only covers the PROJECT side. Explicitly tell the user that AdMob bidding also
     needs dashboard-side setup on levelplay.com (link AdMob account, enable bidding per
     ad unit) that you cannot verify from local files.

9. BUILD AND VERIFY THE ACTUAL APK — don't stop at "it compiled"
   - A successful Gradle build (`BUILD SUCCESSFUL` in Editor.log) does NOT guarantee a
     working app — see step 4. After any real build, use the Android SDK tools bundled
     with the Unity install (`.../PlaybackEngines/AndroidPlayer/SDK/build-tools/<ver>/`)
     to check the actual artifact:
     - `aapt2 dump badging app.apk` → confirm `launchable-activity`, correct
       `package name`, `native-code` covers `arm64-v8a`/`armeabi-v7a`.
     - `aapt2 dump xmltree app.apk --file AndroidManifest.xml` → confirm the AdMob
       APPLICATION_ID meta-data actually made it into the compiled manifest, not just
       your source file.
     - `apksigner verify -v app.apk` → confirms it's actually signed and installable.
   - Ads themselves (real fills) can only be confirmed on a real device — LevelPlay
     returns no real fills in the Unity Editor, ever. Say this explicitly so the user
     doesn't waste time testing in Play Mode.

10. BEFORE HANDING BACK, GIVE A HONEST STATUS TABLE, not a blanket "done"
    - Separate what's verified (console clean, keys entered, manifest checked, real APK
      inspected) from what's still unverified (real ad fills on device, AdMob bidding
      dashboard config, iOS if no iOS keys were given) from what's genuinely optional
      (extra ad networks like Facebook/Chartboost adapters that came bundled but aren't
      configured).
````

---

## 📄 License

See [LICENSE](LICENSE).

---

*Interstitial · Rewarded · Banner · App Open — LevelPlay (ironSource) + AdMob bidding · Android + iOS*