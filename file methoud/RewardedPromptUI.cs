using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RewardedPromptUI — attach to a Canvas Panel in your scene.
///
/// ╔══════════════════════════════════════════════════════════════╗
/// ║  SETUP                                                       ║
/// ╠══════════════════════════════════════════════════════════════╣
/// ║  1. Create a Canvas (Screen Space - Overlay, Sort Order 99) ║
/// ║  2. Add a Panel child — this is the overlay root            ║
/// ║  3. Inside the Panel add:                                   ║
/// ║       • Image         → rewardIconImage                     ║
/// ║       • TextMeshPro   → rewardTextLabel  ("Watch Ad…")      ║
/// ║       • TextMeshPro   → countdownLabel   ("Offer in 5s")    ║
/// ║       • Button        → watchButton      ("Watch Ad")       ║
/// ║       • Button        → declineButton    ("No Thanks")      ║
/// ║  4. Attach THIS script to the Panel root GameObject         ║
/// ║  5. Wire all 5 references in the Inspector                  ║
/// ║                                                              ║
/// ║  ⚠ CRITICAL: Set the Panel GameObject INACTIVE              ║
/// ║    in the Hierarchy before pressing Play.                   ║
/// ║    (Uncheck the checkbox next to its name)                  ║
/// ║    AdManager activates it automatically when needed.        ║
/// ║    If the Panel is ACTIVE at start, Awake() never runs      ║
/// ║    and the singleton won't be registered.                   ║
/// ║                                                              ║
/// ║  You do NOT wire any onClick events manually.               ║
/// ║  All listeners are wired in Awake().                        ║
/// ╚══════════════════════════════════════════════════════════════╝
/// </summary>
public class RewardedPromptUI : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────
    // SINGLETON
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Singleton instance.
    /// NOTE: Because the Panel starts INACTIVE, we cannot rely on
    /// Awake() being called before AdManager needs the instance.
    /// AdManager calls EnsureAwake() before accessing Instance
    /// to force initialization if needed.
    /// </summary>
    public static RewardedPromptUI Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    // INSPECTOR REFERENCES
    // ─────────────────────────────────────────────────────────────
    [Header("UI References — wire ALL of these in the Inspector")]
    [SerializeField] private Image           rewardIconImage;
    [SerializeField] private TextMeshProUGUI rewardTextLabel;
    [SerializeField] private TextMeshProUGUI countdownLabel;
    [SerializeField] private Button          watchButton;
    [SerializeField] private Button          declineButton;

    [Header("Optional Animations")]
    [Tooltip("Animator on the Panel root. Trigger names below must match your Animator.")]
    [SerializeField] private Animator panelAnimator;
    [SerializeField] private string   showTrigger      = "Show";
    [SerializeField] private string   hideTrigger      = "Hide";
    [Tooltip("Seconds to wait for the hide animation before deactivating the GameObject.")]
    [SerializeField] private float    hideAnimDuration = 0.3f;

    // ─────────────────────────────────────────────────────────────
    // STATE
    // ─────────────────────────────────────────────────────────────
    private Action    _onWatch;
    private Action    _onDecline;
    private Coroutine _countdownCoroutine;
    private Coroutine _hideCoroutine;
    private bool      _isShowing;
    private bool      _awakeRan;   // guard against double-init

    /// <summary>True while the overlay is visible on screen.</summary>
    public bool IsVisible => _isShowing;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        DoAwake();
    }

    /// <summary>
    /// Separated from Awake() so it can be called manually when the
    /// GameObject is inactive (Unity does NOT call Awake on inactive objects
    /// unless they were once active). AdManager calls this before accessing
    /// Instance to guarantee initialization.
    ///
    /// Fix: Always keep the Panel INACTIVE at edit-time. Unity WILL call
    /// Awake() the first time the object is activated at runtime.
    /// This method is a safety net only.
    /// </summary>
    private void DoAwake()
    {
        if (_awakeRan) return;
        _awakeRan = true;

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure the panel starts hidden
        // (handles the case where a developer accidentally left it active)
        gameObject.SetActive(false);
        _isShowing = false;

        // Wire button listeners once — no manual onClick wiring needed
        if (watchButton   != null) watchButton  .onClick.AddListener(HandleWatchClicked);
        if (declineButton != null) declineButton.onClick.AddListener(HandleDeclineClicked);

        ValidateReferences();
    }

    // ─────────────────────────────────────────────────────────────
    // PUBLIC API  (called by AdManager)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Activates the overlay and populates it with the reward offer data.
    /// Safe to call while already visible — it resets with the new data.
    ///
    /// The countdown display is driven by <paramref name="countdownSeconds"/>.
    /// Actual expiry (hide + callback) is owned by AdManager.AutoExpirePrompt —
    /// this script does NOT call Hide() on timer end to avoid dual-timer desync.
    /// </summary>
    /// <param name="rewardText">Short description e.g. "+50 Gems" or "2× Coins".</param>
    /// <param name="rewardIcon">Sprite shown beside the text. Pass null to hide icon.</param>
    /// <param name="countdownSeconds">Display-only timer. 0 = no countdown label.</param>
    /// <param name="onWatch">Invoked when the user taps Watch.</param>
    /// <param name="onDecline">Invoked when the user taps Decline.</param>
    public void Show(
        string rewardText,
        Sprite rewardIcon,
        float  countdownSeconds,
        Action onWatch,
        Action onDecline)
    {
        // Cancel any running coroutines from a previous show call
        StopAllRunningCoroutines();

        _onWatch   = onWatch;
        _onDecline = onDecline;
        _isShowing = true;

        // ── Populate reward text ──────────────────────────────────
        if (rewardTextLabel != null)
            rewardTextLabel.text = rewardText;

        // ── Populate reward icon ──────────────────────────────────
        if (rewardIconImage != null)
        {
            bool hasIcon = rewardIcon != null;
            rewardIconImage.gameObject.SetActive(hasIcon);
            if (hasIcon) rewardIconImage.sprite = rewardIcon;
        }

        // ── Countdown label ───────────────────────────────────────
        if (countdownLabel != null)
        {
            bool showCountdown = countdownSeconds > 0f;
            countdownLabel.gameObject.SetActive(showCountdown);

            if (showCountdown)
            {
                countdownLabel.text = BuildCountdownText(Mathf.CeilToInt(countdownSeconds));
                _countdownCoroutine = StartCoroutine(RunCountdownCoroutine(countdownSeconds));
            }
        }

        // ── Activate the panel ────────────────────────────────────
        gameObject.SetActive(true);

        // Play show animation if an Animator is wired
        if (panelAnimator != null && !string.IsNullOrEmpty(showTrigger))
            panelAnimator.SetTrigger(showTrigger);
    }

    /// <summary>
    /// Hides the overlay without invoking any callback.
    /// AdManager calls this when the countdown expires or another ad starts.
    /// </summary>
    public void Hide()
    {
        if (!_isShowing) return;

        StopAllRunningCoroutines();
        _isShowing = false;
        _onWatch   = null;
        _onDecline = null;

        if (panelAnimator != null && !string.IsNullOrEmpty(hideTrigger))
        {
            panelAnimator.SetTrigger(hideTrigger);
            _hideCoroutine = StartCoroutine(DeactivateAfterDelay(hideAnimDuration));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // BUTTON HANDLERS
    // ─────────────────────────────────────────────────────────────

    private void HandleWatchClicked()
    {
        if (!_isShowing) return;

        // Cache and clear BEFORE hiding — prevents a re-entrant Show()
        // call in the callback from being stomped by Hide()
        Action cb = _onWatch;
        Hide();
        cb?.Invoke();
    }

    private void HandleDeclineClicked()
    {
        if (!_isShowing) return;

        Action cb = _onDecline;
        Hide();
        cb?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    // COROUTINES
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the countdown label every second — display only.
    /// AdManager.AutoExpirePrompt() owns the actual hide/expire logic.
    /// </summary>
    private IEnumerator RunCountdownCoroutine(float totalSeconds)
    {
        float remaining = totalSeconds;

        while (remaining > 0f)
        {
            if (countdownLabel != null)
                countdownLabel.text = BuildCountdownText(Mathf.CeilToInt(remaining));

            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Do NOT call Hide() here — AdManager owns expiry timing.
        // Just update the label so it reads "0s" in the rare case AdManager
        // is a frame or two behind.
        if (countdownLabel != null && _isShowing)
            countdownLabel.text = BuildCountdownText(0);

        _countdownCoroutine = null;
    }

    /// <summary>Waits for the hide animation, then deactivates the GameObject.</summary>
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
        _hideCoroutine = null;
    }

    // ─────────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────────

    private void StopAllRunningCoroutines()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
    }

    private static string BuildCountdownText(int secondsLeft)
        => secondsLeft > 0 ? $"Offer expires in {secondsLeft}s" : "Offer expired";

    /// <summary>Logs warnings in the Editor if required references are missing.</summary>
    private void ValidateReferences()
    {
#if UNITY_EDITOR
        if (rewardTextLabel == null)
            UnityEngine.Debug.LogError(
                "[RewardedPromptUI] rewardTextLabel is NOT assigned in the Inspector! " +
                "The reward text won't display.", this);
        if (watchButton == null)
            UnityEngine.Debug.LogError(
                "[RewardedPromptUI] watchButton is NOT assigned in the Inspector! " +
                "Users won't be able to watch the ad.", this);
        if (declineButton == null)
            UnityEngine.Debug.LogError(
                "[RewardedPromptUI] declineButton is NOT assigned in the Inspector! " +
                "Users won't be able to decline.", this);
        if (countdownLabel == null)
            UnityEngine.Debug.LogWarning(
                "[RewardedPromptUI] countdownLabel is not assigned — countdown timer won't show.", this);
        if (rewardIconImage == null)
            UnityEngine.Debug.LogWarning(
                "[RewardedPromptUI] rewardIconImage is not assigned — reward icon won't show.", this);
#endif
    }
}
