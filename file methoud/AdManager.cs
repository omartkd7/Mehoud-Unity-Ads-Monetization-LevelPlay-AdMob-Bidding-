// =============================================================================
//  AdManager.cs v4 — LevelPlay SDK (com.unity.services.levelplay)
//  Scary Toys Funtime: Chapter 3  |  Android + iOS
//  Verified against actual SDK API from package source.
//
//  Compiles cleanly with or without LevelPlay installed.
//  When UNITY_LEVELPLAY is defined, all ads activate automatically.
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_LEVELPLAY
using Unity.Services.LevelPlay;
#endif

public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    // ── ADS COMPLETED SYSTEM ───────────────────────────────────────────────
    public enum AdsCompletedType { RewardedEarned, RewardedSkipped, InterstitialClosed, BannerLoaded, AppOpenClosed }
    public class AdsCompletedArgs
    {
        public AdsCompletedType Type;
        public string PlacementName;
        public bool   WasRewarded;
        public string AdInfo;
    }
    public event Action<AdsCompletedArgs> OnAdsCompleted;
    public void AdsCompleted(AdsCompletedType type, string placement = "", bool rewarded = false, string info = "")
    {
        OnAdsCompleted?.Invoke(new AdsCompletedArgs { Type = type, PlacementName = placement, WasRewarded = rewarded, AdInfo = info });
        Debug.Log($"[AdManager] AdsCompleted → {type} | placement={placement}");
    }

    // ── INSPECTOR ──────────────────────────────────────────────────────────
    [Header("App Keys — LevelPlay Dashboard")]
    [SerializeField] private string appKeyAndroid = "273dd52c5";
    [SerializeField] private string appKeyIOS     = "YOUR_IOS_APP_KEY";

    [Header("Ad Unit IDs — LevelPlay Dashboard")]
    [SerializeField] private string rewardedAdUnitId     = "znsmfqt2h5as69qt";
    [SerializeField] private string interstitialAdUnitId = "ozs5onu6yf9p0hgx";
    [SerializeField] private string bannerAdUnitId       = "071t2sdrks71wa1p";

    [Header("Banner")]
    [Tooltip("0=BottomCenter  1=TopCenter")]
    [SerializeField] private int bannerPositionIndex = 0;
    [Tooltip("0=Banner(320x50)  1=Large(320x90)  2=MediumRectangle(300x250)")]
    [SerializeField] private int bannerSizeIndex = 0;

    [Header("Frequency Caps (seconds)")]
    // ⚡ Set to 0 = NO cooldown between interstitials (ad shows every time).
    [SerializeField] private float cooldownTier1 = 0f;
    [SerializeField] private float cooldownTier2 = 0f;
    [SerializeField] private float cooldownTier3 = 0f;

    [Header("Retention Guards")]
    // ⚡ 0 = no 1-day wait, no tutorial wait. High cap = basically unlimited.
    [SerializeField] private int interstitialUnlockAfterDays = 0;
    [SerializeField] private int maxInterstitialsPerSession  = 9999;
    [SerializeField] private int tutorialLevelCount          = 0;

    [Header("⚡ INSTANT MODE")]
    // When true, TryShowInterstitial ignores ALL guards (day/level/session/cooldown)
    // and shows the ad immediately if one is loaded. Use for test buttons.
    [SerializeField] private bool instantShowNoLimits = true;

    [Header("Rewarded Prompt UI (optional — leave RewardedPromptUI absent to skip)")]
    [Tooltip("Seconds the custom Watch/Decline overlay stays up before auto-declining. 0 = no countdown, no auto-expire.")]
    [SerializeField] private float rewardedPromptCountdown = 5f;

    [Header("Debug")]
    [SerializeField] private bool enableTestMode = false;

    // ── PUBLIC TYPES ───────────────────────────────────────────────────────
    public enum UserTier { Unknown, Tier1, Tier2, Tier3 }

    public static class Placement
    {
        public const string ContinueGame    = "continue_game";
        public const string DoubleCoins     = "double_coins";
        public const string ExtraLives      = "extra_lives";
        public const string DailyBonus      = "daily_bonus";
        public const string UnlockContent   = "unlock_content";
        public const string SkipWait        = "skip_wait";
        public const string LevelComplete   = "level_complete";
        public const string MenuTransition  = "menu_transition";
        public const string PostOnboarding  = "post_onboarding";
        public const string PersistentBottom = "persistent_bottom";
        public const string AppForeground   = "app_foreground";
    }

    // ── PRIVATE STATE ──────────────────────────────────────────────────────
#if UNITY_LEVELPLAY
    private LevelPlayRewardedAd     _rewardedAd;
    private LevelPlayInterstitialAd _interstitialAd;
    private LevelPlayBannerAd       _bannerAd;
#endif

    private bool  _sdkInitialized;
    private bool  _bannerLoaded;
    private bool  _bannerVisible;
    private float _lastInterstitialTime = float.MinValue;
    private int   _interstitialsThisSession;
    private int   _sessionNumber;
    private int   _totalLevelsCompleted;
    private int   _currentLevelNumber;

    private UserTier _userTier            = UserTier.Unknown;
    private string   _detectedCountryCode = "";

    private Action _pendingRewardCallback;
    private string _pendingRewardPlacement = "";
    private bool   _rewardGranted;
    private Action _pendingPromptDecline;

    private int   _statRewardedPrompts;
    private int   _statRewardedShown;
    private int   _statRewardedCompleted;
    private int   _statInterstitialShown;
    private int   _statInterstitialClicked;
    private int   _statBannerClicked;
    private float _statTotalAdRevenue;

    private bool  _adFreeServerVerified    = false;
    private bool  _countryVerifiedByServer = false;
    private int   _sdkInitRetryCount;
    private const int MaxSdkInitRetries    = 3;

    public event Action<string> OnRewardGranted;
    public event Action<string> OnRewardSkipped;
    public event Action         OnInterstitialClosed;
    public event Action         OnSDKReady;

    // ── LIFECYCLE ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
#if !UNITY_LEVELPLAY
        Debug.LogError("[AdManager] LevelPlay SDK NOT installed!\n" +
            "Window → Package Manager → Unity Registry → 'LevelPlay'\n" +
            "Install: com.unity.services.levelplay\n" +
            "UNITY_LEVELPLAY define is added automatically.", this);
#endif
    }

    private void Start()
    {
        _sessionNumber        = PlayerPrefs.GetInt("ad_session_count", 0) + 1;
        PlayerPrefs.SetInt("ad_session_count", _sessionNumber);
        _totalLevelsCompleted = PlayerPrefs.GetInt("ad_total_levels", 0);
        StampInstallDate();
        DetectUserTier();
        ValidateConfig();
        InitializeSDK();
    }

    // TEST ONLY — press I in Play mode to force an interstitial right now,
    // without needing to win/lose the game. Remove before release.
    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.I))
        {
            bool shown = ShowInterstitialNow();
            Debug.Log($"[AdManager] TEST — I pressed, ShowInterstitialNow() = {shown}");
        }
#endif
    }

    private void OnDestroy()
    {
        FlushSessionStats();
#if UNITY_LEVELPLAY
        UnregisterEvents();
        _bannerAd?.DestroyAd();
#endif
    }

    private void ValidateConfig()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(appKeyAndroid) || appKeyAndroid.StartsWith("YOUR_"))
            Debug.LogError("[AdManager] appKeyAndroid not configured!", this);
        if (string.IsNullOrEmpty(appKeyIOS) || appKeyIOS.StartsWith("YOUR_"))
            Debug.LogWarning("[AdManager] appKeyIOS not configured — iOS will fail.", this);
        if (string.IsNullOrEmpty(rewardedAdUnitId) || rewardedAdUnitId.StartsWith("YOUR_"))
            Debug.LogWarning("[AdManager] rewardedAdUnitId not configured.", this);
        if (string.IsNullOrEmpty(interstitialAdUnitId) || interstitialAdUnitId.StartsWith("YOUR_"))
            Debug.LogWarning("[AdManager] interstitialAdUnitId not configured.", this);
        if (string.IsNullOrEmpty(bannerAdUnitId) || bannerAdUnitId.StartsWith("YOUR_"))
            Debug.LogWarning("[AdManager] bannerAdUnitId not configured.", this);
#endif
    }

    // ── GDPR / CCPA ────────────────────────────────────────────────────────
    public static bool GetUserConsent() => PlayerPrefs.GetInt("ad_user_consent", 0) == 1;
    public static bool GetDoNotSell()   => PlayerPrefs.GetInt("ad_do_not_sell",   0) == 1;

    public static void SetUserConsent(bool consent)
    {
        PlayerPrefs.SetInt("ad_user_consent", consent ? 1 : 0);
        PlayerPrefs.Save();
#if UNITY_LEVELPLAY
#pragma warning disable CS0618
        LevelPlay.SetConsent(consent);
#pragma warning restore CS0618
#endif
    }

    public static void SetDoNotSell(bool doNotSell)
    {
        PlayerPrefs.SetInt("ad_do_not_sell", doNotSell ? 1 : 0);
        PlayerPrefs.Save();
#if UNITY_LEVELPLAY
        LevelPlay.SetMetaData("do_not_sell", doNotSell ? "true" : "false");
#endif
    }

    // ── SDK INITIALIZATION ─────────────────────────────────────────────────
    private void InitializeSDK()
    {
#if !UNITY_LEVELPLAY
        Debug.LogError("[AdManager] SDK not installed — ads disabled.");
        return;
#else
        string appKey = Application.platform == RuntimePlatform.IPhonePlayer ? appKeyIOS : appKeyAndroid;
        if (string.IsNullOrEmpty(appKey) || appKey.StartsWith("YOUR_"))
        {
            Debug.LogError("[AdManager] App Key not set in Inspector.");
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        enableTestMode = true;
#endif
        if (enableTestMode) Debug.LogWarning("[AdManager] ⚠ TEST MODE — disable before release!");

#pragma warning disable CS0618
        LevelPlay.SetConsent(GetUserConsent());
#pragma warning restore CS0618
        LevelPlay.SetMetaData("do_not_sell", GetDoNotSell() ? "true" : "false");

        RegisterEvents();
        LevelPlay.Init(appKey, userId: "");
        Debug.Log($"[AdManager] SDK Init → key={appKey} | tier={_userTier} | country={_detectedCountryCode} | session={_sessionNumber}");
#endif
    }

#if UNITY_LEVELPLAY
    // ── EVENT REGISTRATION ─────────────────────────────────────────────────
    private void RegisterEvents()
    {
        LevelPlay.OnInitSuccess         += HandleSDKInitSuccess;
        LevelPlay.OnInitFailed          += HandleSDKInitFailed;
        LevelPlay.OnImpressionDataReady += HandleImpressionData;
    }

    private void UnregisterEvents()
    {
        LevelPlay.OnInitSuccess         -= HandleSDKInitSuccess;
        LevelPlay.OnInitFailed          -= HandleSDKInitFailed;
        LevelPlay.OnImpressionDataReady -= HandleImpressionData;
        UnsubRewarded();
        UnsubInterstitial();
        UnsubBanner();
    }

    private void SubRewarded()
    {
        if (_rewardedAd == null) return;
        _rewardedAd.OnAdLoaded        += HandleRvLoaded;
        _rewardedAd.OnAdLoadFailed    += HandleRvLoadFail;
        _rewardedAd.OnAdDisplayed     += HandleRvOpen;
        _rewardedAd.OnAdDisplayFailed += HandleRvShowFail;
        _rewardedAd.OnAdRewarded      += HandleRvGranted;
        _rewardedAd.OnAdClosed        += HandleRvClosed;
        _rewardedAd.OnAdClicked       += HandleRvClicked;
    }

    private void UnsubRewarded()
    {
        if (_rewardedAd == null) return;
        _rewardedAd.OnAdLoaded        -= HandleRvLoaded;
        _rewardedAd.OnAdLoadFailed    -= HandleRvLoadFail;
        _rewardedAd.OnAdDisplayed     -= HandleRvOpen;
        _rewardedAd.OnAdDisplayFailed -= HandleRvShowFail;
        _rewardedAd.OnAdRewarded      -= HandleRvGranted;
        _rewardedAd.OnAdClosed        -= HandleRvClosed;
        _rewardedAd.OnAdClicked       -= HandleRvClicked;
    }

    private void SubInterstitial()
    {
        if (_interstitialAd == null) return;
        _interstitialAd.OnAdLoaded        += HandleIsReady;
        _interstitialAd.OnAdLoadFailed    += HandleIsLoadFail;
        _interstitialAd.OnAdDisplayed     += HandleIsOpen;
        _interstitialAd.OnAdDisplayFailed += HandleIsShowFail;
        _interstitialAd.OnAdClosed        += HandleIsClosed;
        _interstitialAd.OnAdClicked       += HandleIsClicked;
    }

    private void UnsubInterstitial()
    {
        if (_interstitialAd == null) return;
        _interstitialAd.OnAdLoaded        -= HandleIsReady;
        _interstitialAd.OnAdLoadFailed    -= HandleIsLoadFail;
        _interstitialAd.OnAdDisplayed     -= HandleIsOpen;
        _interstitialAd.OnAdDisplayFailed -= HandleIsShowFail;
        _interstitialAd.OnAdClosed        -= HandleIsClosed;
        _interstitialAd.OnAdClicked       -= HandleIsClicked;
    }

    private void SubBanner()
    {
        if (_bannerAd == null) return;
        _bannerAd.OnAdLoaded        += HandleBannerLoaded;
        _bannerAd.OnAdLoadFailed    += HandleBannerLoadFail;
        _bannerAd.OnAdClicked       += HandleBannerClicked;
        _bannerAd.OnAdDisplayed     += HandleBannerDisplayed;
        _bannerAd.OnAdDisplayFailed += HandleBannerDisplayFail;
    }

    private void UnsubBanner()
    {
        if (_bannerAd == null) return;
        _bannerAd.OnAdLoaded        -= HandleBannerLoaded;
        _bannerAd.OnAdLoadFailed    -= HandleBannerLoadFail;
        _bannerAd.OnAdClicked       -= HandleBannerClicked;
        _bannerAd.OnAdDisplayed     -= HandleBannerDisplayed;
        _bannerAd.OnAdDisplayFailed -= HandleBannerDisplayFail;
    }

    // ── SDK HANDLERS ───────────────────────────────────────────────────────
    private void HandleSDKInitSuccess(LevelPlayConfiguration cfg)
    {
        _sdkInitialized = true;
        Debug.Log($"[AdManager] ✓ SDK READY | {cfg}");
        LoadRewarded();
        LoadInterstitial();
        LoadBanner();
        OnSDKReady?.Invoke();
    }

    private void HandleSDKInitFailed(LevelPlayInitError e)
    {
        Debug.LogError($"[AdManager] ✗ SDK FAILED: {e}");
        if (_sdkInitRetryCount < MaxSdkInitRetries)
            StartCoroutine(RetryInit(30f));
        else
            Debug.LogError("[AdManager] Max retries reached — ads disabled.");
    }

    private IEnumerator RetryInit(float delay)
    {
        _sdkInitRetryCount++;
        yield return new WaitForSeconds(delay);
        UnregisterEvents();
        InitializeSDK();
    }

    private void HandleImpressionData(LevelPlayImpressionData d)
    {
        if (d != null) Debug.Log($"[AdManager] Impression | {d.AllData}");
    }

    // ── REWARDED ───────────────────────────────────────────────────────────
    // FIX: wrapper lambdas so Backoff(Action) gets a proper Action, not a method group
    private void ReloadRewarded()     => LoadRewarded();
    private void ReloadInterstitial() => LoadInterstitial();
    private void ReloadBanner()       => LoadBanner();

    public void LoadRewarded(string id = null)
    {
        if (!_sdkInitialized) return;
        string u = string.IsNullOrEmpty(id) ? rewardedAdUnitId : id;
        if (string.IsNullOrEmpty(u) || u.StartsWith("YOUR_")) { Debug.LogWarning("[AdManager] LoadRewarded — unit ID not set."); return; }
        UnsubRewarded();
        _rewardedAd = new LevelPlayRewardedAd(u);
        SubRewarded();
        _rewardedAd.LoadAd();
        Debug.Log($"[AdManager] Rewarded loading… ({u})");
    }

    private void HandleRvLoaded(LevelPlayAdInfo a)    => Debug.Log($"[AdManager] Rewarded LOADED | {a}");
    private void HandleRvLoadFail(LevelPlayAdError e) { Debug.LogWarning($"[AdManager] Rewarded LOAD FAIL: {e}"); StartCoroutine(Backoff(ReloadRewarded, 30f)); }
    private void HandleRvOpen(LevelPlayAdInfo a)      { _rewardGranted = false; _statRewardedShown++; }
    private void HandleRvClicked(LevelPlayAdInfo a)   { }

    private void HandleRvShowFail(LevelPlayAdInfo a, LevelPlayAdError e)
    {
        Debug.LogError($"[AdManager] Rewarded SHOW FAIL: {e}");
        string p = _pendingRewardPlacement;
        _pendingRewardCallback = null; _pendingRewardPlacement = "";
        OnRewardSkipped?.Invoke(p);
        AdsCompleted(AdsCompletedType.RewardedSkipped, p, false, a.ToString());
        InvokePendingDecline();
        LoadRewarded();
    }

    private void HandleRvGranted(LevelPlayAdInfo a, LevelPlayReward r)
    {
        _rewardGranted = true; _statRewardedCompleted++;
        Debug.Log($"[AdManager] Reward GRANTED | {r}");
        Action cb = _pendingRewardCallback;
        string p  = _pendingRewardPlacement;
        _pendingRewardCallback = null; _pendingRewardPlacement = "";
        _pendingPromptDecline = null; // accepted — clear without invoking
        cb?.Invoke();
        OnRewardGranted?.Invoke(p);
        AdsCompleted(AdsCompletedType.RewardedEarned, p, true, a.ToString());
    }

    private void HandleRvClosed(LevelPlayAdInfo a)
    {
        if (!_rewardGranted)
        {
            string p = _pendingRewardPlacement;
            _pendingRewardCallback = null; _pendingRewardPlacement = "";
            OnRewardSkipped?.Invoke(p);
            AdsCompleted(AdsCompletedType.RewardedSkipped, p, false, a.ToString());
            InvokePendingDecline();
        }
        _rewardGranted = false;
        LoadRewarded();
    }

    // FIX: when ShowRewardedPrompt falls back to the native ad (no RewardedPromptUI in
    // scene), onDeclined was never stored anywhere, so a skipped/closed/failed rewarded
    // ad silently dropped the decline callback — game flow stalled forever after a loss
    // (no revive, no lose panel). _pendingPromptDecline now carries onDeclined through
    // that path too; this fires it exactly once and clears it.
    private void InvokePendingDecline()
    {
        Action decline = _pendingPromptDecline;
        _pendingPromptDecline = null;
        decline?.Invoke();
    }

    // ── INTERSTITIAL ───────────────────────────────────────────────────────
    public void LoadInterstitial(string id = null)
    {
        if (!_sdkInitialized) return;
        string u = string.IsNullOrEmpty(id) ? interstitialAdUnitId : id;
        if (string.IsNullOrEmpty(u) || u.StartsWith("YOUR_")) { Debug.LogWarning("[AdManager] LoadInterstitial — unit ID not set."); return; }
        UnsubInterstitial();
        _interstitialAd = new LevelPlayInterstitialAd(u);
        SubInterstitial();
        _interstitialAd.LoadAd();
        Debug.Log($"[AdManager] Interstitial loading… ({u})");
    }

    private void HandleIsReady(LevelPlayAdInfo a)     => Debug.Log($"[AdManager] Interstitial READY | {a}");
    private void HandleIsLoadFail(LevelPlayAdError e) { Debug.LogWarning($"[AdManager] IS LOAD FAIL: {e}"); StartCoroutine(Backoff(ReloadInterstitial, 30f)); }
    private void HandleIsOpen(LevelPlayAdInfo a)      { _statInterstitialShown++; }
    private void HandleIsClicked(LevelPlayAdInfo a)   { _statInterstitialClicked++; }

    private void HandleIsShowFail(LevelPlayAdInfo a, LevelPlayAdError e)
    {
        Debug.LogError($"[AdManager] IS SHOW FAIL: {e}");
        if (_interstitialsThisSession > 0) _interstitialsThisSession--;
        LoadInterstitial();
    }

    private void HandleIsClosed(LevelPlayAdInfo a)
    {
        LoadInterstitial();
        OnInterstitialClosed?.Invoke();
        AdsCompleted(AdsCompletedType.InterstitialClosed, "", false, a.ToString());
    }

    // ── BANNER ─────────────────────────────────────────────────────────────
    public void LoadBanner(string id = null)
    {
        if (!_sdkInitialized) return;
        string u = string.IsNullOrEmpty(id) ? bannerAdUnitId : id;
        if (string.IsNullOrEmpty(u) || u.StartsWith("YOUR_")) { Debug.LogWarning("[AdManager] LoadBanner — unit ID not set."); return; }

        UnsubBanner();

        // Correct API: LevelPlayAdSize.LARGE (not LARGE_BANNER), LevelPlayBannerPosition (not LevelPlayAdPosition)
        var pos  = bannerPositionIndex == 1 ? LevelPlayBannerPosition.TopCenter : LevelPlayBannerPosition.BottomCenter;
        var size = bannerSizeIndex == 1     ? LevelPlayAdSize.LARGE
                 : bannerSizeIndex == 2     ? LevelPlayAdSize.MEDIUM_RECTANGLE
                 :                            LevelPlayAdSize.BANNER;

        var cfg = new LevelPlayBannerAd.Config.Builder()
            .SetSize(size)
            .SetPosition(pos)
            .SetDisplayOnLoad(true)
            .Build();

        _bannerAd = new LevelPlayBannerAd(u, cfg);
        SubBanner();
        _bannerAd.LoadAd();
        Debug.Log($"[AdManager] Banner loading… ({u})");
    }

    private void HandleBannerLoaded(LevelPlayAdInfo a)
    {
        _bannerLoaded = true; _bannerAd?.ShowAd(); _bannerVisible = true;
        Debug.Log($"[AdManager] Banner LOADED | {a}");
        AdsCompleted(AdsCompletedType.BannerLoaded, "", false, a.ToString());
    }

    private void HandleBannerLoadFail(LevelPlayAdError e)
    { _bannerLoaded = false; Debug.LogWarning($"[AdManager] Banner LOAD FAIL: {e}"); StartCoroutine(Backoff(ReloadBanner, 60f)); }

    private void HandleBannerClicked(LevelPlayAdInfo a)   { _statBannerClicked++; }
    private void HandleBannerDisplayed(LevelPlayAdInfo a) { }
    private void HandleBannerDisplayFail(LevelPlayAdInfo a, LevelPlayAdError e) => Debug.LogWarning($"[AdManager] Banner DISPLAY FAIL: {e}");

#endif // UNITY_LEVELPLAY

    // ── PUBLIC API (safe to call without SDK) ──────────────────────────────
    public bool IsRewardedReady()
    {
#if UNITY_LEVELPLAY
        return _sdkInitialized && _rewardedAd != null && _rewardedAd.IsAdReady();
#else
        return false;
#endif
    }

    public bool IsInterstitialReady()
    {
#if UNITY_LEVELPLAY
        return _sdkInitialized && _interstitialAd != null && _interstitialAd.IsAdReady();
#else
        return false;
#endif
    }

    public bool IsBannerVisible() => _bannerVisible;

    public bool ShowRewarded(string placement, Action onRewarded = null)
    {
#if UNITY_LEVELPLAY
        if (!_sdkInitialized)   { Debug.LogWarning("[AdManager] ShowRewarded — SDK not ready."); return false; }
        if (!IsRewardedReady()) { Debug.LogWarning("[AdManager] ShowRewarded — no ad ready."); LoadRewarded(); return false; }
        _pendingRewardCallback  = onRewarded;
        _pendingRewardPlacement = placement;
        _rewardGranted          = false;
        _statRewardedPrompts++;
        _rewardedAd.ShowAd(placement);
        return true;
#else
        return false;
#endif
    }

    public void ShowRewardedPrompt(string placement, Action onAccepted, Action onDeclined = null)
    {
        if (!IsRewardedReady()) { onDeclined?.Invoke(); return; }

        if (RewardedPromptUI.Instance == null)
        {
            _pendingPromptDecline = onDeclined;
            ShowRewarded(placement, onAccepted);
            return;
        }

        _pendingPromptDecline = onDeclined;
        RewardedPromptUI.Instance.Show(
            rewardText:       GetPromptRewardText(placement),
            rewardIcon:       null,
            countdownSeconds: rewardedPromptCountdown,
            onWatch: () =>
            {
                _pendingPromptDecline = null;
                ShowRewarded(placement, onAccepted);
            },
            onDecline: () =>
            {
                _pendingPromptDecline = null;
                onDeclined?.Invoke();
            });

        if (rewardedPromptCountdown > 0f)
            StartCoroutine(AutoExpirePrompt(rewardedPromptCountdown));
    }

    private static string GetPromptRewardText(string placement) => placement switch
    {
        Placement.ContinueGame  => "Watch an ad to continue",
        Placement.DoubleCoins   => "Watch an ad to double your coins",
        Placement.ExtraLives    => "Watch an ad for an extra life",
        Placement.DailyBonus    => "Watch an ad for your daily bonus",
        Placement.SkipWait      => "Watch an ad to skip the wait",
        Placement.UnlockContent => "Watch an ad to unlock",
        _                       => "Watch an ad for a reward"
    };

    // AutoExpirePrompt owns hide+decline on timeout — RewardedPromptUI only
    // updates its own countdown label and never self-hides on expiry.
    private IEnumerator AutoExpirePrompt(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (RewardedPromptUI.Instance != null && RewardedPromptUI.Instance.IsVisible)
        {
            RewardedPromptUI.Instance.Hide();
            Action cb = _pendingPromptDecline;
            _pendingPromptDecline = null;
            cb?.Invoke();
        }
    }

    public bool TryShowInterstitial(string placement)
    {
#if UNITY_LEVELPLAY
        if (IsAdFree()) return false;
        if (!_sdkInitialized || _interstitialAd == null || !_interstitialAd.IsAdReady()) { LoadInterstitial(); return false; }

        // ⚡ INSTANT MODE — skip every retention guard, show right away.
        if (!instantShowNoLimits)
        {
            if (GetDaysSinceInstall() < interstitialUnlockAfterDays)     return false;
            if (_totalLevelsCompleted < tutorialLevelCount)              return false;
            if (_interstitialsThisSession >= maxInterstitialsPerSession) return false;
            if (Time.realtimeSinceStartup - _lastInterstitialTime < GetIsCooldown()) return false;
        }

        _interstitialAd.ShowAd(placement);
        _lastInterstitialTime = Time.realtimeSinceStartup;
        _interstitialsThisSession++;
        return true;
#else
        return false;
#endif
    }

    // ⚡ Call this DIRECTLY from a UI button to force an interstitial NOW.
    // Bypasses day/level/session/cooldown guards regardless of instantShowNoLimits.
    // Returns false only if the SDK isn't ready or no ad is loaded yet.
    public bool ShowInterstitialNow(string placement = Placement.MenuTransition)
    {
#if UNITY_LEVELPLAY
        if (!_sdkInitialized || _interstitialAd == null || !_interstitialAd.IsAdReady())
        {
            Debug.LogWarning("[AdManager] ShowInterstitialNow — no ad loaded yet, loading…");
            LoadInterstitial();
            return false;
        }
        _interstitialAd.ShowAd(placement);
        _lastInterstitialTime = Time.realtimeSinceStartup;
        _interstitialsThisSession++;
        return true;
#else
        return false;
#endif
    }

    // ⚡ Call this DIRECTLY from a UI button to force a rewarded ad NOW (no prompt overlay).
    public bool ShowRewardedNow(string placement = Placement.DoubleCoins, Action onRewarded = null)
        => ShowRewarded(placement, onRewarded);

    public void ShowBanner()
    {
#if UNITY_LEVELPLAY
        if (!_bannerLoaded || _bannerVisible || _bannerAd == null) return;
        _bannerAd.ShowAd(); _bannerVisible = true;
#endif
    }

    public void HideBanner()
    {
#if UNITY_LEVELPLAY
        if (!_bannerVisible || _bannerAd == null) return;
        _bannerAd.HideAd(); _bannerVisible = false;
#endif
    }

    public void DestroyBanner()
    {
#if UNITY_LEVELPLAY
        if (_bannerAd == null) return;
        UnsubBanner(); _bannerAd.DestroyAd();
        _bannerAd = null; _bannerLoaded = false; _bannerVisible = false;
#endif
    }

    // ── TIER DETECTION ─────────────────────────────────────────────────────
    public bool IsCountryVerified() => _countryVerifiedByServer;

    private void DetectUserTier()
    {
        _detectedCountryCode = FetchCountryCode().ToUpperInvariant();
        var t1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "US","GB","AU","CA","DE","JP","FR","KR","NL","NO","SE","DK","CH","NZ","FI","SG","HK","AT","IE","BE" };
        var t2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BR","MX","IN","RU","TR","PL","ES","IT","AR","ZA","TH","MY","CZ","HU","RO","UA","SA","AE","IL","GR" };
        _userTier = t1.Contains(_detectedCountryCode) ? UserTier.Tier1
                  : t2.Contains(_detectedCountryCode) ? UserTier.Tier2
                  : UserTier.Tier3;
        Debug.Log($"[AdManager] Tier={_userTier} | Country={_detectedCountryCode}");
    }

    private string FetchCountryCode()
    {
        string v = PlayerPrefs.GetString("geo_country_verified", "");
        if (!string.IsNullOrEmpty(v)) { _countryVerifiedByServer = true; return v; }
        _countryVerifiedByServer = false;
        try { return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; }
        catch { return "ZZ"; }
    }

    public void SetServerCountryCode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        string u = code.ToUpperInvariant();
        PlayerPrefs.SetString("geo_country_verified", u);
        PlayerPrefs.SetString("geo_country", u);
        PlayerPrefs.Save();
        _countryVerifiedByServer = true;
        _detectedCountryCode = u;
        DetectUserTier();
    }

    private float GetIsCooldown() => _userTier switch
    {
        UserTier.Tier1 => cooldownTier1,
        UserTier.Tier2 => cooldownTier2,
        UserTier.Tier3 => cooldownTier3,
        _              => cooldownTier1
    };

    public UserTier GetUserTier()    => _userTier;
    public string   GetCountryCode() => _detectedCountryCode;

    // ── HELPERS ────────────────────────────────────────────────────────────
    private IEnumerator Backoff(Action fn, float baseDelay)
    {
        float d = baseDelay;
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(d);
            if (!_sdkInitialized) yield break;
            fn?.Invoke();
            yield return null;
            yield return null;
            d = Mathf.Min(d * 2f, 300f);
        }
    }

    public int GetDaysSinceInstall()
    {
        string r = PlayerPrefs.GetString("ad_install_date", "");
        if (string.IsNullOrEmpty(r)) { StampInstallDate(); return 0; }
        return DateTime.TryParse(r, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime d)
            ? Mathf.Max(0, (int)(DateTime.UtcNow - d).TotalDays) : 0;
    }

    private void StampInstallDate()
    {
        if (!PlayerPrefs.HasKey("ad_install_date"))
        { PlayerPrefs.SetString("ad_install_date", DateTime.UtcNow.ToString("O")); PlayerPrefs.Save(); }
    }

    public AdSessionStats GetSessionStats() => new AdSessionStats
    {
        SessionNumber        = _sessionNumber,
        Country              = _detectedCountryCode,
        Tier                 = _userTier,
        RewardedPrompts      = _statRewardedPrompts,
        RewardedShown        = _statRewardedShown,
        RewardedCompleted    = _statRewardedCompleted,
        InterstitialsShown   = _statInterstitialShown,
        InterstitialsClicked = _statInterstitialClicked,
        BannerClicked        = _statBannerClicked,
        TotalAdRevenue       = _statTotalAdRevenue,
        RewardedOptInRate      = _statRewardedPrompts > 0 ? (float)_statRewardedShown / _statRewardedPrompts : 0f,
        RewardedCompletionRate = _statRewardedShown   > 0 ? (float)_statRewardedCompleted / _statRewardedShown : 0f
    };

    private void FlushSessionStats()
    {
        var s = GetSessionStats();
        Debug.Log($"[AdManager] SESSION END | RV opt-in={s.RewardedOptInRate:P1} | IS={s.InterstitialsShown} | Rev=${s.TotalAdRevenue:F4}");
        PlayerPrefs.SetFloat("stat_lifetime_revenue", PlayerPrefs.GetFloat("stat_lifetime_revenue", 0f) + _statTotalAdRevenue);
        PlayerPrefs.Save();
    }

    // ── GAME HOOKS ─────────────────────────────────────────────────────────
    public void OnLevelComplete(int n)
    {
        _currentLevelNumber   = n;
        _totalLevelsCompleted = Mathf.Max(_totalLevelsCompleted, n);
        PlayerPrefs.SetInt("ad_total_levels", _totalLevelsCompleted);
        PlayerPrefs.Save();
        if (n <= tutorialLevelCount) return;
        TryShowInterstitial(Placement.LevelComplete);
    }

    public void OnGameOver(Action onRevived, Action onDied = null)
        => ShowRewardedPrompt(Placement.ContinueGame, onRevived, onDied);

    public void OnDoubleRewardOffer(string lbl, Action onDoubled, Action onSkip = null)
        => ShowRewardedPrompt(Placement.DoubleCoins, onDoubled, onSkip);

    public void OnDailyBonusOffer(Action onClaimed)
        => ShowRewardedPrompt(Placement.DailyBonus, onClaimed);

    public void OnSkipTimerOffer(string lbl, Action onSkip)
        => ShowRewardedPrompt(Placement.SkipWait, onSkip);

    public void OnMenuOpen()      { TryShowInterstitial(Placement.MenuTransition); ShowBanner(); }
    public void OnGameplayStart() => HideBanner();

    public void OnOnboardingComplete()
    {
        _totalLevelsCompleted = Mathf.Max(_totalLevelsCompleted, tutorialLevelCount);
        PlayerPrefs.SetInt("ad_total_levels", _totalLevelsCompleted);
        PlayerPrefs.Save();
        TryShowInterstitial(Placement.PostOnboarding);
    }

    // ── AD-FREE IAP GUARD ─────────────────────────────────────────────────
    // Fully local: a purchase recorded via SetAdFree() stays ad-free forever
    // (PlayerPrefs), no backend round-trip required. SetAdFreeVerified() is
    // an OPTIONAL extra confirmation for projects that add a receipt-
    // validation server later — IsAdFree() does not require it.
    public void SetAdFree()
    {
        PlayerPrefs.SetInt("ad_free_purchased", 1);
        PlayerPrefs.Save();
        HideBanner();
        DestroyBanner();
        Debug.Log("[AdManager] Ad-free activated (local).");
    }

    public void SetAdFreeVerified(bool ok)
    {
        _adFreeServerVerified = ok;
        PlayerPrefs.SetInt("ad_free_server_verified", ok ? 1 : 0);
        PlayerPrefs.Save();
        if (ok)
        {
            Debug.Log("[AdManager] Ad-free server-verified.");
        }
        else
        {
            PlayerPrefs.SetInt("ad_free_purchased", 0);
            PlayerPrefs.Save();
            Debug.LogWarning("[AdManager] Ad-free REVOKED.");
#if UNITY_LEVELPLAY
            LoadBanner();
#endif
            ShowBanner();
        }
    }

    public bool IsAdFree()
    {
        if (_adFreeServerVerified) return true;
        if (PlayerPrefs.GetInt("ad_free_server_verified", 0) == 1) { _adFreeServerVerified = true; return true; }
        return PlayerPrefs.GetInt("ad_free_purchased", 0) == 1;
    }
}

// ── SESSION STATS ──────────────────────────────────────────────────────────
[Serializable]
public class AdSessionStats
{
    public int SessionNumber; public string Country; public AdManager.UserTier Tier;
    public int RewardedPrompts; public int RewardedShown; public int RewardedCompleted;
    public float RewardedOptInRate; public float RewardedCompletionRate;
    public int InterstitialsShown; public int InterstitialsClicked;
    public int BannerClicked; public float TotalAdRevenue;
    public override string ToString() =>
        $"Session {SessionNumber} [{Country}/{Tier}] | RV opt-in {RewardedOptInRate:P1} | IS {InterstitialsShown} | Rev ${TotalAdRevenue:F4}";
}
