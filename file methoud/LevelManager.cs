// =============================================================================
//  LevelManager.cs — Scary Toys Funtime: Chapter 3
//  Thin gameplay-side call sites for AdManager (Banner / Interstitial / Rewarded).
//  Does NOT do frequency capping itself — AdManager has the final say
//  (grace period, session cap, min interval, tier cooldown).
// =============================================================================
using System;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int levelIndex = 1;

    private void Start()
    {
        // Persistent bottom banner while this scene is up (menu / level select).
        // Call AdManager.Instance.HideBanner() from your gameplay scene's Start()
        // if you don't want the banner during actual play.
        AdManager.Instance.ShowBanner();
    }

    // ── BANNER ───────────────────────────────────────────────────────────
    public void ShowBannerAd() => AdManager.Instance.ShowBanner();
    public void HideBannerAd() => AdManager.Instance.HideBanner();

    // ── INTERSTITIAL ─────────────────────────────────────────────────────
    /// <summary>Call when the player finishes a level. AdManager decides whether
    /// an interstitial actually shows (tutorial guard, session cap, cooldown).</summary>
    public void OnLevelCompleted()
    {
        AdManager.Instance.OnLevelComplete(levelIndex);
        Debug.Log($"[LevelManager] Level {levelIndex} complete.");
        ProceedToNextLevel();
    }

    /// <summary>Call from a menu button / scene transition to try an interstitial
    /// outside the level-complete flow. Returns false if AdManager declined to show it.</summary>
    public bool ShowInterstitialAd(string placement = AdManager.Placement.MenuTransition)
        => AdManager.Instance.TryShowInterstitial(placement);

    /// <summary>⚡ Wire a UI Button's OnClick to THIS to force an interstitial
    /// immediately — no cooldown, no 1-day wait, no level/session limits.</summary>
    public void OnButtonShowInterstitial()
        => AdManager.Instance.ShowInterstitialNow(AdManager.Placement.MenuTransition);

    /// <summary>⚡ Wire a UI Button's OnClick to THIS to force a rewarded ad
    /// immediately (no Watch/Decline overlay).</summary>
    public void OnButtonShowRewarded()
        => AdManager.Instance.ShowRewardedNow(AdManager.Placement.DoubleCoins, onRewarded: RewardThePlayer);

    private void RewardThePlayer()
        => Debug.Log("[LevelManager] Rewarded ad completed — give the player their reward here.");

    // ── REWARDED ─────────────────────────────────────────────────────────
    /// <summary>
    /// The method to call to show a rewarded ad. Shows the custom
    /// RewardedPromptUI overlay first (if one exists in the scene) so the
    /// player can Watch/Decline, then plays the native ad on Watch.
    /// Falls back straight to the native ad if no overlay is wired up.
    /// </summary>
    /// <param name="placement">Use one of the AdManager.Placement constants
    /// (ContinueGame, DoubleCoins, ExtraLives, DailyBonus, SkipWait, UnlockContent…).</param>
    /// <param name="onRewardGranted">Called only if the player watches to completion.</param>
    /// <param name="onRewardDeclined">Called if the player declines, closes early,
    /// or no ad is available.</param>
    public void CallRewardedAd(string placement, Action onRewardGranted, Action onRewardDeclined = null)
    {
        if (!AdManager.Instance.IsRewardedReady())
        {
            Debug.LogWarning($"[LevelManager] Rewarded not ready for '{placement}' — declining.");
            onRewardDeclined?.Invoke();
            return;
        }

        AdManager.Instance.ShowRewardedPrompt(placement, onAccepted: onRewardGranted, onDeclined: onRewardDeclined);
    }

    /// <summary>Convenience wrapper for the most common rewarded flow: "watch ad to continue" on death.</summary>
    public void OnContinueWithRewarded()
        => CallRewardedAd(AdManager.Placement.ContinueGame, onRewardGranted: RevivePlayer, onRewardDeclined: GoToMenu);

    /// <summary>Convenience wrapper: "watch ad to double coins" offer.</summary>
    public void OnDoubleCoinsWithRewarded(Action onDoubled)
        => CallRewardedAd(AdManager.Placement.DoubleCoins, onRewardGranted: onDoubled);

    // ── Replace these stubs with your real flow ─────────────────────────
    private void ProceedToNextLevel()
    {
        Debug.Log($"[LevelManager] Loading level {levelIndex + 1}...");
        // SceneManager.LoadScene("Level_" + (levelIndex + 1));
    }

    private void RevivePlayer()
    {
        Debug.Log("[LevelManager] Player revived via rewarded ad.");
        // PlayerController.Instance.Revive();
    }

    private void GoToMenu()
    {
        Debug.Log("[LevelManager] No revive — returning to menu.");
        // SceneManager.LoadScene("MainMenu");
    }
}
