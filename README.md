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

---

## 📄 License

See [LICENSE](LICENSE).

---

*Interstitial · Rewarded · Banner · App Open — LevelPlay (ironSource) + AdMob bidding · Android + iOS*
