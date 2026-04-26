using Enemy;
using UnityEngine;
using UnityEngine.Events;

namespace Audio
{
    /// <summary>
    /// Watches all EnemyAI awareness states. Fires onCombatEnter when any enemy
    /// first becomes aware (after a period of none being aware). Fires onCombatExit
    /// when no enemy has been aware for exitCooldown seconds. The cooldown debounces
    /// brief breaks in contact so music doesn't ping-pong.
    /// </summary>
    public class EnemyAwarenessTracker : MonoBehaviour
    {
        [Tooltip("How many times per second to scan for aware enemies.")]
        [SerializeField] private float scanRateHz = 5f;
        [Tooltip("Seconds of continuous unawareness required before combat-exit fires.")]
        [SerializeField] private float exitCooldown = 4.5f;

        public UnityEvent onCombatEnter;
        public UnityEvent onCombatExit;

        private bool _cachedAnyAware;
        private bool _inCombat;
        private float _scanTimer;
        private float _exitTimer;

        private void Update()
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer >= 1f / Mathf.Max(1f, scanRateHz))
            {
                _scanTimer = 0f;
                _cachedAnyAware = AnyEnemyAware();
            }

            if (_cachedAnyAware)
            {
                _exitTimer = 0f;
                if (!_inCombat)
                {
                    _inCombat = true;
                    onCombatEnter?.Invoke();
                }
            }
            else if (_inCombat)
            {
                _exitTimer += Time.deltaTime;
                if (_exitTimer >= exitCooldown)
                {
                    _inCombat = false;
                    _exitTimer = 0f;
                    onCombatExit?.Invoke();
                }
            }
        }

        private bool AnyEnemyAware()
        {
            EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyAI e = enemies[i];
                if (e != null && e.isActiveAndEnabled && e.IsAwareOfPlayer) return true;
            }
            return false;
        }
    }
}
