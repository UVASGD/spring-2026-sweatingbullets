using Enemy;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Applies an instant nerves spike when a fired shot narrowly misses a visible, alive enemy.
    /// </summary>
    public class NearMissNervesInput : NervesInput
    {
        [Header("Near-Miss Settings")]
        [SerializeField] private float nearMissAngleDegrees = 6f;
        [SerializeField] private float nearMissSpikeAmount = 12f;
        [SerializeField] private WeaponController weaponController;

        private float _pendingSpike;

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInParent<WeaponController>();

            if (weaponController == null)
                Debug.LogError("NearMissNervesInput: Could not find WeaponController in parent hierarchy.");
        }

        private void OnEnable()
        {
            if (weaponController != null)
                weaponController.OnWeaponShotResolved += HandleWeaponShotResolved;
        }

        private void OnDisable()
        {
            if (weaponController != null)
                weaponController.OnWeaponShotResolved -= HandleWeaponShotResolved;
        }

        protected override float CalculateNervesDelta()
        {
            if (_pendingSpike <= 0f)
                return 0f;

            float spike = _pendingSpike;
            _pendingSpike = 0f;
            Debug.Log($"[NearMiss] CalculateNervesDelta: consuming spike={spike}");
            return spike;
        }

        private void HandleWeaponShotResolved(WeaponController.ShotResolutionContext shotContext)
        {
            Debug.Log($"[NearMiss] HandleWeaponShotResolved called. hitEnemy={shotContext.HitEnemy} spikeAmount={nearMissSpikeAmount}");

            if (shotContext.HitEnemy || nearMissSpikeAmount <= 0f)
            {
                Debug.Log($"[NearMiss] Skipping near-miss check. hitEnemy={shotContext.HitEnemy} spikeAmount={nearMissSpikeAmount}");
                return;
            }

            if (shotContext.Range <= 0f || shotContext.Direction.sqrMagnitude <= Mathf.Epsilon)
            {
                Debug.LogWarning("[NearMiss] Invalid shot context: range or direction is zero.");
                return;
            }

            Vector3 shotDirection = shotContext.Direction.normalized;

            EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            Debug.Log($"[NearMiss] Checking {enemies.Length} enemies for near miss.");
            float bestAngle = float.MaxValue;
            bool hasNearMiss = false;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyAI enemy = enemies[i];
                if (enemy == null || enemy.isDead || !enemy.isActiveAndEnabled)
                {
                    Debug.Log($"[NearMiss] Skipping enemy {enemy?.name ?? "null"}: dead or disabled.");
                    continue;
                }

                if (!TryGetEnemyCollider(enemy, out Collider enemyCollider))
                {
                    Debug.LogWarning($"[NearMiss] No collider found on enemy {enemy.name}, skipping.");
                    continue;
                }

                Vector3 projectedPoint = ProjectPointOntoShotSegment(
                    shotContext.Origin,
                    shotDirection,
                    shotContext.Range,
                    enemy.transform.position);

                Vector3 closestPoint = enemyCollider.ClosestPoint(projectedPoint);
                Vector3 toClosestPoint = closestPoint - shotContext.Origin;
                if (toClosestPoint.sqrMagnitude <= Mathf.Epsilon)
                {
                    Debug.Log($"[NearMiss] Enemy {enemy.name} closest point is at origin, skipping.");
                    continue;
                }

                float missAngle = Vector3.Angle(shotDirection, toClosestPoint);
                bool withinAngle = missAngle <= nearMissAngleDegrees;
                bool hasLOS = withinAngle && HasLineOfSight(shotContext.Origin, closestPoint, enemy);

                Debug.Log($"[NearMiss] Enemy={enemy.name} angle={missAngle:F2}° threshold={nearMissAngleDegrees}° withinAngle={withinAngle} LOS={hasLOS}");

                if (!withinAngle)
                    continue;

                if (!hasLOS)
                    continue;

                if (missAngle < bestAngle)
                {
                    bestAngle = missAngle;
                    hasNearMiss = true;
                }
            }

            if (hasNearMiss)
            {
                Debug.Log($"[NearMiss] Near miss detected! Spike={nearMissSpikeAmount} bestAngle={bestAngle:F2}°. Pending spike is now {_pendingSpike + nearMissSpikeAmount}.");
                _pendingSpike += nearMissSpikeAmount;
            }
            else
            {
                Debug.Log("[NearMiss] No near miss detected.");
            }
        }

        private static Vector3 ProjectPointOntoShotSegment(
            Vector3 rayOrigin,
            Vector3 rayDirection,
            float rayRange,
            Vector3 point)
        {
            float distanceOnRay = Vector3.Dot(point - rayOrigin, rayDirection);
            float clampedDistance = Mathf.Clamp(distanceOnRay, 0f, rayRange);
            return rayOrigin + rayDirection * clampedDistance;
        }

        private static bool TryGetEnemyCollider(EnemyAI enemy, out Collider enemyCollider)
        {
            enemyCollider = enemy.GetComponent<Collider>();
            if (enemyCollider != null)
                return true;

            enemyCollider = enemy.GetComponentInChildren<Collider>();
            return enemyCollider != null;
        }

        private static bool HasLineOfSight(Vector3 shotOrigin, Vector3 targetPoint, EnemyAI targetEnemy)
        {
            Vector3 direction = targetPoint - shotOrigin;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
                return false;

            if (!Physics.Raycast(shotOrigin, direction / distance, out RaycastHit hit, distance))
                return false;

            EnemyAI hitEnemy = hit.collider.GetComponentInParent<EnemyAI>();
            return hitEnemy == targetEnemy;
        }
    }
}
