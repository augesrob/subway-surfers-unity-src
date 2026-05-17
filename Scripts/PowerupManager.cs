using System.Collections;
using UnityEngine;

/// <summary>
/// Singleton that receives TikTok gift triggers via Unity SendMessage from JavaScript.
/// Add this script to a GameObject named exactly "PowerupManager" in the scene.
/// JS calls:  SendMessage("PowerupManager", "ForceSpawnPickupType", "Jetpack")
/// </summary>
public class PowerupManager : MonoBehaviour
{
    public static PowerupManager Instance;

    [Header("Powerup Durations (seconds)")]
    [SerializeField] private float _jetpackDuration    = 8f;
    [SerializeField] private float _magnetDuration     = 10f;
    [SerializeField] private float _hoverboardDuration = 8f;
    [SerializeField] private float _multiplierDuration = 10f;
    [SerializeField] private float _sneakersDuration   = 8f;
    [SerializeField] private float _speedDuration      = 5f;

    [Header("Powerup Values")]
    [SerializeField] private float _jetpackJumpForce   = 18f;
    [SerializeField] private float _sneakersJumpForce  = 22f;
    [SerializeField] private float _speedScale         = 1.6f;
    [SerializeField] private int   _magnetCoinsPerTick = 8;
    [SerializeField] private int   _multiplierBonus    = 15;
    [SerializeField] private int   _coinRowAmount      = 50;

    private ForceReceiver  _forceReceiver;
    private bool _jetpackActive    = false;
    private bool _magnetActive     = false;
    private bool _hoverboardActive = false;
    private bool _sneakersActive   = false;
    private bool _multiplierActive = false;

    // ────────────────────────────────────────────────────────────
    // Lifecycle
    // ────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        // Grab ForceReceiver from the player
        var player = FindAnyObjectByType<PlayerController>();
        if (player != null)
            _forceReceiver = player.GetComponent<ForceReceiver>();
    }

    // ────────────────────────────────────────────────────────────
    // SendMessage entry points  (called from JavaScript)
    // All must be: public void MethodName(string param)
    // ────────────────────────────────────────────────────────────

    /// <summary>Spawns/activates the named powerup.</summary>
    public void ForceSpawnPickupType(string type)
    {
        switch (type.Trim())
        {
            case "Jetpack":       StartCoroutine(JetpackRoutine());    break;
            case "CoinMagnet":    StartCoroutine(MagnetRoutine());     break;
            case "Hoverboard":    StartCoroutine(HoverboardRoutine()); break;
            case "Multiplier":    StartCoroutine(MultiplierRoutine()); break;
            case "SuperSneakers": StartCoroutine(SneakersRoutine());   break;
            default:
                Debug.Log("[PowerupManager] Unknown type: " + type);
                break;
        }
    }

    /// <summary>Instantly adds coins (simulates a coin row pickup).</summary>
    public void SpawnCoinRows(string countStr)
    {
        int rows = ParseInt(countStr, 3);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.IncreaseCoin(rows * _coinRowAmount);
    }

    /// <summary>Adds a flat number of coins.</summary>
    public void SpawnCoins(string countStr)
    {
        int count = ParseInt(countStr, 20);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.IncreaseCoin(count);
    }

    /// <summary>Alias for SpawnCoins.</summary>
    public void AddCoins(string countStr) => SpawnCoins(countStr);

    /// <summary>Temporarily changes game speed. Resets automatically.</summary>
    public void SetTimeScale(string scaleStr)
    {
        if (float.TryParse(scaleStr, out float scale))
        {
            scale = Mathf.Clamp(scale, 0.1f, 3f);
            Time.timeScale = scale;
            float dur = scale > 1f ? _speedDuration : _speedDuration * 0.5f;
            StartCoroutine(ResetTimeScaleAfter(dur));
        }
    }

    /// <summary>Speed boost shortcut.</summary>
    public void SpeedBoost(string unused)
    {
        Time.timeScale = _speedScale;
        StartCoroutine(ResetTimeScaleAfter(_speedDuration));
    }

    /// <summary>Slow-motion shortcut.</summary>
    public void SlowMotion(string unused)
    {
        Time.timeScale = 0.4f;
        StartCoroutine(ResetTimeScaleAfter(_speedDuration * 0.8f));
    }

    // ────────────────────────────────────────────────────────────
    // Powerup coroutines
    // ────────────────────────────────────────────────────────────

    private IEnumerator JetpackRoutine()
    {
        if (_jetpackActive) yield break;
        _jetpackActive = true;
        Debug.Log("[PowerupManager] Jetpack ON");

        float elapsed = 0f;
        float interval = 0.25f;
        while (elapsed < _jetpackDuration)
        {
            // Keep player airborne with repeated jumps
            if (_forceReceiver != null && _forceReceiver.IsGrounded)
                _forceReceiver.Jump(_jetpackJumpForce);

            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }

        _jetpackActive = false;
        Debug.Log("[PowerupManager] Jetpack OFF");
    }

    private IEnumerator MagnetRoutine()
    {
        if (_magnetActive) yield break;
        _magnetActive = true;
        Debug.Log("[PowerupManager] Magnet ON");

        float elapsed = 0f;
        float tick = 0.4f;
        while (elapsed < _magnetDuration)
        {
            // Simulate attracting coins by adding them directly
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.IncreaseCoin(_magnetCoinsPerTick);

            elapsed += tick;
            yield return new WaitForSeconds(tick);
        }

        _magnetActive = false;
        Debug.Log("[PowerupManager] Magnet OFF");
    }

    private IEnumerator HoverboardRoutine()
    {
        if (_hoverboardActive) yield break;
        _hoverboardActive = true;
        Debug.Log("[PowerupManager] Hoverboard ON");

        // Brief slow-mo on activation (feels like activating a board)
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(0.35f);
        Time.timeScale = 1f;

        // Give coin bonus to represent the board's value
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.IncreaseCoin(75);

        yield return new WaitForSeconds(_hoverboardDuration);
        _hoverboardActive = false;
        Debug.Log("[PowerupManager] Hoverboard OFF");
    }

    private IEnumerator MultiplierRoutine()
    {
        if (_multiplierActive) yield break;
        _multiplierActive = true;
        Debug.Log("[PowerupManager] x2 Multiplier ON");

        float elapsed = 0f;
        float tick = 0.5f;
        while (elapsed < _multiplierDuration)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.IncreaseCoin(_multiplierBonus);

            elapsed += tick;
            yield return new WaitForSeconds(tick);
        }

        _multiplierActive = false;
        Debug.Log("[PowerupManager] x2 Multiplier OFF");
    }

    private IEnumerator SneakersRoutine()
    {
        if (_sneakersActive) yield break;
        _sneakersActive = true;
        Debug.Log("[PowerupManager] SuperSneakers ON");

        float elapsed = 0f;
        float interval = 0.6f;
        while (elapsed < _sneakersDuration)
        {
            if (_forceReceiver != null && _forceReceiver.IsGrounded)
                _forceReceiver.Jump(_sneakersJumpForce);

            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }

        _sneakersActive = false;
        Debug.Log("[PowerupManager] SuperSneakers OFF");
    }

    private IEnumerator ResetTimeScaleAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Time.timeScale = 1f;
    }

    // ────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────
    private static int ParseInt(string s, int fallback)
    {
        return int.TryParse(s, out int v) ? v : fallback;
    }
}
