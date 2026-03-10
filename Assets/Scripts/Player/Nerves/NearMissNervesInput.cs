using System.Collections.Generic;
using Enemy;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Applies an instant nerves spike when a fired shot appears to narrowly miss an enemy on screen.
    /// </summary>
    public class NearMissNervesInput : NervesInput
    {
        [Header("Near-Miss Settings")]
        [Tooltip("Maximum viewport-space distance from screen center for a shot to count as a near miss.")]
        [SerializeField] private float missRadius = 0.08f;
        [Tooltip("Smallest nerves spike that can be applied when a near miss is detected.")]
        [SerializeField] private float minSpike = 2f;
        [Tooltip("Largest nerves spike that can be applied for an extremely close near miss.")]
        [SerializeField] private float maxSpike = 12f;
        [Tooltip("Shapes how fast the spike grows as the miss gets closer to the screen center.")]
        [SerializeField] private AnimationCurve missCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Tooltip("Shapes how strongly nearby enemies amplify the spike compared with distant enemies.")]
        [SerializeField] private AnimationCurve distanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        [Tooltip("Enemy distance at or below this uses the strongest distance weighting.")]
        [SerializeField] private float nearDistance = 5f;
        [Tooltip("Enemy distance at or beyond this uses the weakest distance weighting.")]
        [SerializeField] private float farDistance = 30f;
        [Tooltip("When enabled, NearMissTriggerVolume colliders are included as near-miss helper geometry.")]
        [SerializeField] private bool useVolumes = true;
        [Tooltip("Logs the chosen near-miss candidate and scoring details for tuning.")]
        [SerializeField] private bool debugNearMiss = false;
        [Tooltip("Weapon controller that publishes resolved player shots for near-miss evaluation.")]
        [SerializeField] private WeaponController weaponController;

        private readonly List<Collider> _candidateColliders = new List<Collider>();
        private float _pendingSpike;

        private readonly struct NearMissCandidate
        {
            public NearMissCandidate(
                EnemyAI enemy,
                Vector3 point,
                float screenMiss,
                float missStrength,
                float distanceStrength,
                float worldDistance,
                float spike)
            {
                Enemy = enemy;
                Point = point;
                ScreenMiss = screenMiss;
                MissStrength = missStrength;
                DistanceStrength = distanceStrength;
                WorldDistance = worldDistance;
                Spike = spike;
            }

            public EnemyAI Enemy { get; }
            public Vector3 Point { get; }
            public float ScreenMiss { get; }
            public float MissStrength { get; }
            public float DistanceStrength { get; }
            public float WorldDistance { get; }
            public float Spike { get; }
        }

        private readonly struct ClosestPointCandidate
        {
            public ClosestPointCandidate(Vector3 point)
            {
                Point = point;
            }

            public Vector3 Point { get; }
        }

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            missRadius = Mathf.Max(0f, missRadius);
            minSpike = Mathf.Max(0f, minSpike);
            maxSpike = Mathf.Max(minSpike, maxSpike);
            nearDistance = Mathf.Max(0f, nearDistance);
            farDistance = Mathf.Max(nearDistance, farDistance);
        }
#endif

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
            if (shotContext.HitEnemy || maxSpike <= 0f || missRadius <= 0f)
                return;

            if (shotContext.Range <= 0f || shotContext.Direction.sqrMagnitude <= Mathf.Epsilon)
            {
                if (debugNearMiss)
                    Debug.LogWarning("[NearMiss] Invalid shot context: range or direction is zero.");
                return;
            }

            if (!TryResolveCamera(out Camera shotCamera))
                return;

            if (!TryFindBestNearMiss(shotContext, shotCamera, out NearMissCandidate candidate))
                return;

            _pendingSpike += candidate.Spike;

            if (debugNearMiss)
            {
                Debug.Log(
                    $"[NearMiss] Enemy={candidate.Enemy.name}, ScreenMiss={candidate.ScreenMiss:F4}, " +
                    $"MissStrength={candidate.MissStrength:F2}, DistanceStrength={candidate.DistanceStrength:F2}, " +
                    $"WorldDistance={candidate.WorldDistance:F2}, Spike={candidate.Spike:F2}");
            }
        }

        private bool TryResolveCamera(out Camera shotCamera)
        {
            shotCamera = weaponController != null ? weaponController.playerCamera : null;
            if (shotCamera != null)
                return true;

            shotCamera = Camera.main;
            if (shotCamera != null)
                return true;

            Debug.LogWarning("NearMissNervesInput: No player camera available for near-miss evaluation.");
            return false;
        }

        private bool TryFindBestNearMiss(
            WeaponController.ShotResolutionContext shotContext,
            Camera shotCamera,
            out NearMissCandidate bestCandidate)
        {
            bestCandidate = default;

            Vector3 shotDirection = shotContext.Direction.normalized;
            float shotLimit = shotContext.HitSomething
                ? Mathf.Min(shotContext.HitDistance, shotContext.Range)
                : shotContext.Range;

            if (shotLimit <= Mathf.Epsilon)
                return false;

            EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            bool foundCandidate = false;
            float bestScreenMiss = float.MaxValue;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyAI enemy = enemies[i];
                if (!IsValidEnemy(enemy))
                    continue;

                if (!TryGetClosestPointOnEnemy(enemy, shotContext.Origin, shotDirection, shotLimit, out ClosestPointCandidate pointCandidate))
                    continue;

                if (!HasLineOfSight(shotContext.Origin, pointCandidate.Point, enemy))
                    continue;

                Vector3 viewportPoint = shotCamera.WorldToViewportPoint(pointCandidate.Point);
                if (viewportPoint.z <= 0f)
                    continue;

                float screenMiss = Vector2.Distance(
                    new Vector2(viewportPoint.x, viewportPoint.y),
                    new Vector2(0.5f, 0.5f));

                if (screenMiss > missRadius)
                    continue;

                float missStrength = EvaluateMissStrength(screenMiss);
                float worldDistance = Vector3.Distance(shotContext.Origin, pointCandidate.Point);
                float distanceStrength = EvaluateDistanceStrength(worldDistance);
                float spike = Mathf.Lerp(minSpike, maxSpike, missStrength * distanceStrength);

                if (!foundCandidate || screenMiss < bestScreenMiss)
                {
                    bestScreenMiss = screenMiss;
                    bestCandidate = new NearMissCandidate(
                        enemy,
                        pointCandidate.Point,
                        screenMiss,
                        missStrength,
                        distanceStrength,
                        worldDistance,
                        spike);
                    foundCandidate = true;
                }
            }

            return foundCandidate;
        }

        private bool TryGetClosestPointOnEnemy(
            EnemyAI enemy,
            Vector3 shotOrigin,
            Vector3 shotDirection,
            float shotLimit,
            out ClosestPointCandidate bestCandidate)
        {
            bestCandidate = default;
            CollectCandidateColliders(enemy, _candidateColliders);
            if (_candidateColliders.Count == 0)
                return false;

            bool foundCandidate = false;
            float bestOffsetSqr = float.MaxValue;

            for (int i = 0; i < _candidateColliders.Count; i++)
            {
                Collider candidateCollider = _candidateColliders[i];
                if (candidateCollider == null || !candidateCollider.enabled)
                    continue;

                Vector3 projectedPoint = ProjectPointOntoShotSegment(
                    shotOrigin,
                    shotDirection,
                    shotLimit,
                    candidateCollider.bounds.center);

                Vector3 closestPoint = candidateCollider.ClosestPoint(projectedPoint);
                Vector3 toClosestPoint = closestPoint - shotOrigin;
                float distanceAlongShot = Vector3.Dot(toClosestPoint, shotDirection);
                if (distanceAlongShot <= 0f || distanceAlongShot > shotLimit)
                    continue;

                Vector3 pointOnShot = shotOrigin + shotDirection * distanceAlongShot;
                float offsetSqr = (closestPoint - pointOnShot).sqrMagnitude;

                if (!foundCandidate || offsetSqr < bestOffsetSqr)
                {
                    bestOffsetSqr = offsetSqr;
                    bestCandidate = new ClosestPointCandidate(closestPoint);
                    foundCandidate = true;
                }
            }

            return foundCandidate;
        }

        private void CollectCandidateColliders(EnemyAI enemy, List<Collider> colliders)
        {
            colliders.Clear();

            if (useVolumes)
            {
                NearMissTriggerVolume[] volumes = enemy.GetComponentsInChildren<NearMissTriggerVolume>(true);
                for (int i = 0; i < volumes.Length; i++)
                {
                    NearMissTriggerVolume volume = volumes[i];
                    if (volume == null || !volume.IsActiveForNearMiss)
                        continue;

                    Collider volumeCollider = volume.GetComponent<Collider>();
                    if (volumeCollider != null && volumeCollider.enabled)
                        colliders.Add(volumeCollider);
                }
            }

            Collider[] enemyColliders = enemy.GetComponentsInChildren<Collider>();
            for (int i = 0; i < enemyColliders.Length; i++)
            {
                Collider candidateCollider = enemyColliders[i];
                if (candidateCollider != null && candidateCollider.enabled && !candidateCollider.isTrigger)
                    colliders.Add(candidateCollider);
            }
        }

        private float EvaluateMissStrength(float screenMiss)
        {
            float missT = 1f - Mathf.Clamp01(screenMiss / missRadius);
            return Mathf.Clamp01(EvaluateCurve(missCurve, missT, missT));
        }

        private float EvaluateDistanceStrength(float worldDistance)
        {
            if (farDistance <= nearDistance)
                return 1f;

            float distanceT = Mathf.InverseLerp(nearDistance, farDistance, worldDistance);
            return Mathf.Clamp01(EvaluateCurve(distanceCurve, distanceT, 1f - distanceT));
        }

        private static float EvaluateCurve(AnimationCurve curve, float time, float fallback)
        {
            if (curve == null || curve.length == 0)
                return fallback;

            return curve.Evaluate(time);
        }

        private static bool IsValidEnemy(EnemyAI enemy)
        {
            return enemy != null && enemy.isActiveAndEnabled && !enemy.isDead;
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
                return true;

            EnemyAI hitEnemy = hit.collider.GetComponentInParent<EnemyAI>();
            return hitEnemy == targetEnemy;
        }
    }
}
