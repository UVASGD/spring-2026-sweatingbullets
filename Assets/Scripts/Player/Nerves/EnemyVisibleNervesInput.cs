using System.Collections.Generic;
using Enemy;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Player
{
    /// <summary>
    /// Pressure model: enters "under pressure" when an enemy is both visible to
    /// the player AND aware of the player. While under pressure, sustained nerves
    /// tick — using the visible-aware weighted rate when applicable, or a fallback
    /// rate when the player has broken LOS but enemies are still aware. Pressure
    /// ends when no enemy is aware of the player. The first-sight spike fires
    /// when each unique enemy is first observed while aware.
    /// </summary>
    public class EnemyVisibleNervesInput : NervesInput
    {
        [Header("References")]
        [SerializeField] private WeaponController weaponController;
        [Tooltip("Layers that block line of sight. If the player's collider is on a layer here, exclude it.")]
        [SerializeField] private LayerMask losBlockerMask = ~0;

        [Header("Visibility Detection")]
        [Tooltip("Padding around the viewport [0,1]. Larger = peripheral enemies still count.")]
        [SerializeField] private float viewportPadding = 0.05f;
        [SerializeField] private float maxVisibilityDistance = 80f;
        [Tooltip("Re-scan visibility this many times per second.")]
        [SerializeField] private float scanRateHz = 10f;

        [Header("Sustained Rate")]
        [SerializeField] private float baseSustainedRate = 8f;
        [Tooltip("Distance scaling: x = normalized distance (0=close, 1=max), y = multiplier.")]
        [SerializeField] private AnimationCurve distanceScale = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
        [Tooltip("Each visible enemy beyond the first multiplies the rate by (1 + this).")]
        [SerializeField] private float additionalEnemyMultiplier = 0.4f;
        [Tooltip("Multiplier when an enemy is dead-centered on screen. 1 = no bonus.")]
        [SerializeField] private float centerednessBonus = 1.3f;
        [Tooltip("Rate while under pressure but no aware enemies are currently visible (you've turned and run). Fraction of baseSustainedRate.")]
        [SerializeField, Range(0f, 2f)] private float outOfSightPressureMultiplier = 0.6f;

        [Header("First-Sight Spike")]
        [SerializeField] private float firstSightSpike = 6f;
        [Tooltip("Fires once per first-sighting of an aware enemy. Wire to a stinger AudioSource.")]
        public UnityEvent onFirstSighting;

        private readonly HashSet<int> _everSeenEnemyIds = new();
        private float _scanTimer;
        private float _cachedRatePerSecond;
        private float _pendingSpike;
        private bool _underPressure;

        private void Reset()
        {
            maxContribution = 50f;
        }

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInParent<WeaponController>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _everSeenEnemyIds.Clear();
            _cachedRatePerSecond = 0f;
            _pendingSpike = 0f;
            _scanTimer = 0f;
            _underPressure = false;
        }

        protected override float CalculateNervesDelta()
        {
            _scanTimer += Time.deltaTime;
            float scanInterval = 1f / Mathf.Max(1f, scanRateHz);
            if (_scanTimer >= scanInterval)
            {
                _scanTimer = 0f;
                Rescan();
            }

            float delta = _cachedRatePerSecond * Time.deltaTime;
            if (_pendingSpike > 0f)
            {
                delta += _pendingSpike;
                _pendingSpike = 0f;
            }
            return delta;
        }

        private void Rescan()
        {
            _cachedRatePerSecond = 0f;

            Camera cam = weaponController != null ? weaponController.playerCamera : Camera.main;
            if (cam == null) return;

            EnemyAI[] enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            int visibleAwareCount = 0;
            float weightSum = 0f;
            bool anyAware = false;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyAI e = enemies[i];
                if (e == null || !e.isActiveAndEnabled || e.isDead) continue;

                bool isAware = e.IsAwareOfPlayer;
                if (isAware) anyAware = true;

                Vector3 targetPoint = e.eyes != null
                    ? e.eyes.transform.position
                    : e.transform.position + Vector3.up * 1.5f;

                Vector3 vp = cam.WorldToViewportPoint(targetPoint);
                if (vp.z <= 0f) continue;
                if (vp.x < -viewportPadding || vp.x > 1f + viewportPadding) continue;
                if (vp.y < -viewportPadding || vp.y > 1f + viewportPadding) continue;

                Vector3 camPos = cam.transform.position;
                Vector3 toTarget = targetPoint - camPos;
                float worldDist = toTarget.magnitude;
                if (worldDist > maxVisibilityDistance) continue;

                if (Physics.Raycast(camPos, toTarget.normalized, out RaycastHit hit,
                        worldDist + 0.5f, losBlockerMask, QueryTriggerInteraction.Ignore))
                {
                    EnemyAI hitEnemy = hit.collider.GetComponentInParent<EnemyAI>();
                    if (hitEnemy != e) continue;
                }

                // Visible. Only contributes to pressure if aware.
                if (!isAware) continue;

                visibleAwareCount++;

                float distT = Mathf.Clamp01(worldDist / maxVisibilityDistance);
                float distMult = distanceScale.Evaluate(distT);

                Vector2 centerOffset = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                float centerT = 1f - Mathf.Clamp01(centerOffset.magnitude * 2f);
                float centerMult = Mathf.Lerp(1f, centerednessBonus, centerT);

                weightSum += distMult * centerMult;

                int id = e.GetInstanceID();
                if (_everSeenEnemyIds.Add(id))
                {
                    _pendingSpike += firstSightSpike;
                    onFirstSighting?.Invoke();
                }
            }

            if (visibleAwareCount > 0)
            {
                _underPressure = true;
                float meanWeight = weightSum / visibleAwareCount;
                float countMult = 1f + additionalEnemyMultiplier * (visibleAwareCount - 1);
                _cachedRatePerSecond = baseSustainedRate * meanWeight * countMult;
            }
            else if (_underPressure && anyAware)
            {
                // Player has broken LOS but enemies are still hunting them.
                _cachedRatePerSecond = baseSustainedRate * outOfSightPressureMultiplier;
            }
            else
            {
                _underPressure = false;
            }
        }
    }
}
