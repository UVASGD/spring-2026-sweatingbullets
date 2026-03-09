using Enemy;
using System;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Applies an instant nerves spike when a fired shot passes through an enemy's near-miss volume.
    /// </summary>
    public class NearMissNervesInput : NervesInput
    {
        [Header("Near-Miss Settings")]
        [SerializeField] private float nearMissAngleDegrees = 6f;
        [SerializeField] private float nearMissSpikeAmount = 12f;
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private LayerMask nearMissTriggerMask;
        [SerializeField] private bool enableLegacyFallback = true;

        private float _pendingSpike;

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInParent<WeaponController>();

            if (weaponController == null)
                Debug.LogError("NearMissNervesInput: Could not find WeaponController in parent hierarchy.");

            ResolveNearMissTriggerMask();
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
            return spike;
        }

        private void HandleWeaponShotResolved(WeaponController.ShotResolutionContext shotContext)
        {
            if (shotContext.HitEnemy || nearMissSpikeAmount <= 0f)
                return;

            if (shotContext.Range <= 0f || shotContext.Direction.sqrMagnitude <= Mathf.Epsilon)
            {
                Debug.LogWarning("[NearMiss] Invalid shot context: range or direction is zero.");
                return;
            }

            if (TryDetectTriggerNearMiss(shotContext, out _))
            {
                _pendingSpike += nearMissSpikeAmount;
                return;
            }

            if (enableLegacyFallback && TryDetectLegacyNearMiss(shotContext))
                _pendingSpike += nearMissSpikeAmount;
        }

        private bool TryDetectTriggerNearMiss(
            WeaponController.ShotResolutionContext shotContext,
            out EnemyAI nearMissEnemy)
        {
            nearMissEnemy = null;

            int resolvedMask = ResolveNearMissTriggerMask();
            if (resolvedMask == 0)
                return false;

            RaycastHit[] triggerHits = Physics.RaycastAll(
                shotContext.Origin,
                shotContext.Direction.normalized,
                shotContext.Range,
                resolvedMask,
                QueryTriggerInteraction.Collide);

            if (triggerHits.Length == 0)
                return false;

            Array.Sort(triggerHits, CompareHitDistance);

            float solidHitDistance = shotContext.HitSomething
                ? shotContext.HitDistance
                : float.PositiveInfinity;

            for (int i = 0; i < triggerHits.Length; i++)
            {
                RaycastHit triggerHit = triggerHits[i];
                if (triggerHit.distance >= solidHitDistance)
                    break;

                NearMissTriggerVolume volume = triggerHit.collider.GetComponentInParent<NearMissTriggerVolume>();
                if (volume == null || !volume.IsActiveForNearMiss)
                    continue;

                EnemyAI owner = volume.Owner;
                if (owner == null || owner.isDead || !owner.isActiveAndEnabled)
                    continue;

                nearMissEnemy = owner;
                return true;
            }

            return false;
        }

        private bool TryDetectLegacyNearMiss(WeaponController.ShotResolutionContext shotContext)
        {
            Vector3 shotDirection = shotContext.Direction.normalized;
            EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            float bestAngle = float.MaxValue;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyAI enemy = enemies[i];
                if (enemy == null || enemy.isDead || !enemy.isActiveAndEnabled)
                    continue;

                if (HasActiveNearMissVolume(enemy))
                    continue;

                if (!TryGetEnemyCollider(enemy, out Collider enemyCollider))
                    continue;

                Vector3 projectedPoint = ProjectPointOntoShotSegment(
                    shotContext.Origin,
                    shotDirection,
                    shotContext.Range,
                    enemy.transform.position);

                Vector3 closestPoint = enemyCollider.ClosestPoint(projectedPoint);
                Vector3 toClosestPoint = closestPoint - shotContext.Origin;
                if (toClosestPoint.sqrMagnitude <= Mathf.Epsilon)
                    continue;

                float missAngle = Vector3.Angle(shotDirection, toClosestPoint);
                if (missAngle > nearMissAngleDegrees)
                    continue;

                if (!HasLineOfSight(shotContext.Origin, closestPoint, enemy))
                    continue;

                if (missAngle < bestAngle)
                    bestAngle = missAngle;
            }

            return bestAngle < float.MaxValue;
        }

        private static int CompareHitDistance(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }

        private static bool HasActiveNearMissVolume(EnemyAI enemy)
        {
            NearMissTriggerVolume volume = enemy.GetComponentInChildren<NearMissTriggerVolume>(true);
            return volume != null && volume.IsActiveForNearMiss;
        }

        private int ResolveNearMissTriggerMask()
        {
            if (nearMissTriggerMask.value != 0)
                return nearMissTriggerMask.value;

            int nearMissLayer = LayerMask.NameToLayer("EnemyNearMiss");
            if (nearMissLayer < 0)
                return 0;

            nearMissTriggerMask = 1 << nearMissLayer;
            return nearMissTriggerMask.value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveNearMissTriggerMask();
        }
#endif

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
            Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                {
                    enemyCollider = colliders[i];
                    return true;
                }
            }

            enemyCollider = null;
            return false;
        }

        private static bool HasLineOfSight(Vector3 shotOrigin, Vector3 targetPoint, EnemyAI targetEnemy)
        {
            Vector3 direction = targetPoint - shotOrigin;
            float distance = direction.magnitude;
            if (distance <= Mathf.Epsilon)
                return false;

            if (!Physics.Raycast(
                    shotOrigin,
                    direction / distance,
                    out RaycastHit hit,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                return false;

            EnemyAI hitEnemy = hit.collider.GetComponentInParent<EnemyAI>();
            return hitEnemy == targetEnemy;
        }
    }
}
