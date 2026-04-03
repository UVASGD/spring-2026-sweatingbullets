using UnityEngine;

namespace Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class NearMissTriggerVolume : MonoBehaviour
    {
        [SerializeField] private EnemyAI owner;
        [SerializeField] private bool isActiveForNearMiss = true;

        public EnemyAI Owner => owner;
        public bool IsActiveForNearMiss => isActiveForNearMiss && enabled && gameObject.activeInHierarchy;

        private void Awake()
        {
            ResolveOwner();
            EnsureTriggerCollider();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveOwner();
            EnsureTriggerCollider();
        }
#endif

        private void ResolveOwner()
        {
            if (owner == null)
                owner = GetComponentInParent<EnemyAI>();
        }

        private void EnsureTriggerCollider()
        {
            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }
    }
}
