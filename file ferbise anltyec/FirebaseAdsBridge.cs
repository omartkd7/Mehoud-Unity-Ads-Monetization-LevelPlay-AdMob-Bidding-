// ============================================================
//  FirebaseAdsBridge.cs
//  Bridges AdManager.OnAdsCompleted → FirebaseAnalyticsManager
//  Attach to the same "ads config" GameObject as AdManager.
// ============================================================
using UnityEngine;

public class FirebaseAdsBridge : MonoBehaviour
{
    void OnEnable()
    {
        // Wait for AdManager to be ready then subscribe
        if (AdManager.Instance != null)
            Subscribe();
        else
            // Try again next frame — AdManager may still be initializing
            Invoke(nameof(TrySubscribe), 0.5f);
    }

    void OnDisable()
    {
        if (AdManager.Instance != null)
            AdManager.Instance.OnAdsCompleted -= HandleAdsCompleted;
    }

    void TrySubscribe()
    {
        if (AdManager.Instance != null)
            Subscribe();
    }

    void Subscribe()
    {
        AdManager.Instance.OnAdsCompleted -= HandleAdsCompleted; // guard duplicates
        AdManager.Instance.OnAdsCompleted += HandleAdsCompleted;
        Debug.Log("[FirebaseAdsBridge] Subscribed to OnAdsCompleted");
    }

    void HandleAdsCompleted(AdManager.AdsCompletedArgs args)
    {
        if (FirebaseAnalyticsManager.Instance == null) return;

        FirebaseAnalyticsManager.Instance.LogAdCompleted(
            adType:    args.Type.ToString(),
            placement: args.PlacementName,
            rewarded:  args.WasRewarded
        );
    }
}
