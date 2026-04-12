// ============================================================
//  FirebaseAnalyticsManager.cs — Scary Toys Funtime: Chapter 3
//  Firebase Analytics v13.7.0
//  Tracks: sessions, levels, game over, revive, ads, menus
// ============================================================
using UnityEngine;
using Firebase;
using Firebase.Analytics;
using System.Collections;
using System.Collections.Generic;

public class FirebaseAnalyticsManager : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────
    public static FirebaseAnalyticsManager Instance { get; private set; }

    private bool _isReady = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeFirebase();
    }

    // ── Initialization ─────────────────────────────────────────
    void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var status = task.Result;
            // Marshal back to main thread
            StartCoroutine(OnFirebaseChecked(status));
        });
    }

    IEnumerator OnFirebaseChecked(DependencyStatus status)
    {
        yield return null; // wait one frame for main thread
        if (status == DependencyStatus.Available)
        {
            _isReady = true;
            Debug.Log("[Firebase] ✓ Analytics ready");

            // Identify device
            FirebaseAnalytics.SetUserId(SystemInfo.deviceUniqueIdentifier);
            FirebaseAnalytics.SetUserProperty("platform",
                Application.platform.ToString().ToLower());
            FirebaseAnalytics.SetUserProperty("game_version",
                Application.version);

            // Log app open
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAppOpen);
            Debug.Log("[Firebase] Event: app_open");
        }
        else
        {
            Debug.LogWarning($"[Firebase] ✗ Dependencies not met: {status}");
            _isReady = false;
        }
    }

    // ── Guard ──────────────────────────────────────────────────
    bool Ready(string eventName)
    {
        if (!_isReady)
        {
            Debug.LogWarning($"[Firebase] Not ready — skipping event: {eventName}");
            return false;
        }
        return true;
    }

    // ==========================================================
    // GAME FLOW EVENTS
    // ==========================================================

    // Called once when Splash/first scene loads
    public void LogSessionStart()
    {
        if (!Ready("session_start")) return;
        FirebaseAnalytics.LogEvent("session_start");
        Debug.Log("[Firebase] Event: session_start");
    }

    // Called by MainMenuEventListener.Start()
    public void LogMainMenu()
    {
        if (!Ready("screen_main_menu")) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventScreenView,
            FirebaseAnalytics.ParameterScreenName, "main_menu");
        Debug.Log("[Firebase] Screen: main_menu");
    }

    // Called by MainMenuEventListener.PlayGame()
    public void LogPlayPressed()
    {
        if (!Ready("play_pressed")) return;
        FirebaseAnalytics.LogEvent("play_pressed");
        Debug.Log("[Firebase] Event: play_pressed");
    }

    // Called by LevelSelectionListener
    public void LogLevelSelected(int levelNumber)
    {
        if (!Ready("level_selected")) return;
        FirebaseAnalytics.LogEvent("level_selected",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: level_selected — level {levelNumber}");
    }

    // Called by GameManager.Start()
    public void LogLevelStart(int levelNumber)
    {
        if (!Ready("level_start")) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart,
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: level_start — level {levelNumber}");
    }

    // Called by GameManager.LevelComplete()
    public void LogLevelComplete(int levelNumber)
    {
        if (!Ready("level_end")) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd,
            new Parameter[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelNumber),
                new Parameter(FirebaseAnalytics.ParameterSuccess, 1)
            });
        Debug.Log($"[Firebase] Event: level_end (success) — level {levelNumber}");
    }

    // Called by GameManager.LevelFail()
    public void LogLevelFail(int levelNumber)
    {
        if (!Ready("level_fail")) return;
        FirebaseAnalytics.LogEvent("level_fail",
            new Parameter[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelNumber),
                new Parameter(FirebaseAnalytics.ParameterSuccess, 0)
            });
        Debug.Log($"[Firebase] Event: level_fail — level {levelNumber}");
    }

    // ==========================================================
    // AD EVENTS
    // ==========================================================

    // Called when rewarded ad revive offer appears
    public void LogReviveOfferShown(int levelNumber)
    {
        if (!Ready("revive_offer_shown")) return;
        FirebaseAnalytics.LogEvent("revive_offer_shown",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: revive_offer_shown — level {levelNumber}");
    }

    // Called when player watches ad and revives
    public void LogReviveAccepted(int levelNumber)
    {
        if (!Ready("revive_accepted")) return;
        FirebaseAnalytics.LogEvent("revive_accepted",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: revive_accepted — level {levelNumber}");
    }

    // Called when player declines revive / no ad available
    public void LogReviveDeclined(int levelNumber)
    {
        if (!Ready("revive_declined")) return;
        FirebaseAnalytics.LogEvent("revive_declined",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: revive_declined — level {levelNumber}");
    }

    // Called by AdManager.AdsCompleted for any ad type
    public void LogAdCompleted(string adType, string placement, bool rewarded)
    {
        if (!Ready("ad_completed")) return;
        FirebaseAnalytics.LogEvent("ad_completed",
            new Parameter[]
            {
                new Parameter("ad_type",    adType),
                new Parameter("placement",  placement),
                new Parameter("rewarded",   rewarded ? 1 : 0)
            });
        Debug.Log($"[Firebase] Event: ad_completed — {adType} / {placement}");
    }

    // ==========================================================
    // NAVIGATION EVENTS
    // ==========================================================

    public void LogReplay(int levelNumber)
    {
        if (!Ready("replay")) return;
        FirebaseAnalytics.LogEvent("replay",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: replay — level {levelNumber}");
    }

    public void LogGoHome(int levelNumber)
    {
        if (!Ready("go_home")) return;
        FirebaseAnalytics.LogEvent("go_home",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: go_home — level {levelNumber}");
    }

    public void LogContinue(int levelNumber)
    {
        if (!Ready("continue_to_next")) return;
        FirebaseAnalytics.LogEvent("continue_to_next",
            FirebaseAnalytics.ParameterLevel, levelNumber);
        Debug.Log($"[Firebase] Event: continue_to_next — level {levelNumber}");
    }

    // ==========================================================
    // TUTORIAL / ONBOARDING
    // ==========================================================

    public void LogTutorialComplete()
    {
        if (!Ready("tutorial_complete")) return;
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventTutorialComplete);
        Debug.Log("[Firebase] Event: tutorial_complete");
    }

    // ==========================================================
    // GENERIC HELPERS
    // ==========================================================

    public void LogEvent(string eventName)
    {
        if (!Ready(eventName)) return;
        FirebaseAnalytics.LogEvent(eventName);
        Debug.Log($"[Firebase] Event: {eventName}");
    }

    public void LogEvent(string eventName, string paramName, string paramValue)
    {
        if (!Ready(eventName)) return;
        FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
        Debug.Log($"[Firebase] Event: {eventName} — {paramName}={paramValue}");
    }

    public void LogEvent(string eventName, string paramName, int paramValue)
    {
        if (!Ready(eventName)) return;
        FirebaseAnalytics.LogEvent(eventName, paramName, paramValue);
        Debug.Log($"[Firebase] Event: {eventName} — {paramName}={paramValue}");
    }

    public bool IsReady() => _isReady;
}
