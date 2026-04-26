using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerPickupController : MonoBehaviour
    {
        [SerializeField] private WeaponController weaponController;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private float searchRadius = 4f;
        [SerializeField] private AudioSource pickupAudio;

        private readonly Collider[] _pickupHits = new Collider[16];
        private PickupItem _currentNearest;

        public event Action<PickupItem> OnNearestPickupChanged;
        public PickupItem CurrentNearest => _currentNearest;

        private void Awake()
        {
            if (weaponController == null)
                weaponController = GetComponentInChildren<WeaponController>(true);
            if (pickupAudio == null)
                pickupAudio = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (interactAction == null)
                return;

            interactAction.action.Enable();
            interactAction.action.performed += HandleInteract;
        }

        private void OnDisable()
        {
            if (interactAction == null)
                return;

            interactAction.action.performed -= HandleInteract;
            interactAction.action.Disable();
        }

        private void Update()
        {
            UpdateNearest();

            if (interactAction != null)
                return;

            if ((Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame))
            {
                TryPickupNearest();
            }
        }

        private void HandleInteract(InputAction.CallbackContext context)
        {
            TryPickupNearest();
        }

        private void TryPickupNearest()
        {
            PickupItem nearest = _currentNearest != null ? _currentNearest : FindNearestPickup();
            if (nearest == null)
                return;

            nearest.TryPickup(weaponController, pickupAudio);

            // Destroy() defers until end of frame, so the picked-up collider is
            // still in the physics scene right now. Clear the cache and notify;
            // the next Update() will re-scan once the destruction has finalized.
            _currentNearest = null;
            OnNearestPickupChanged?.Invoke(null);
        }

        private void UpdateNearest()
        {
            PickupItem nearest = FindNearestPickup();

            // If the cached pickup was destroyed externally (e.g., by the gun
            // pickup's "destroy other guns" path), Unity's operator== treats it
            // as null. Emit the change so listeners learn the previous pickup is
            // gone, then continue with the fresh scan.
            if (!ReferenceEquals(_currentNearest, null) && _currentNearest == null)
            {
                _currentNearest = null;
                OnNearestPickupChanged?.Invoke(null);
            }

            if (nearest == _currentNearest)
                return;

            _currentNearest = nearest;
            OnNearestPickupChanged?.Invoke(_currentNearest);
        }

        private PickupItem FindNearestPickup()
        {
            Vector3 origin = transform.position;
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                searchRadius,
                _pickupHits,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);

            PickupItem nearest = null;
            float nearestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = _pickupHits[i];
                if (hit == null)
                    continue;

                PickupItem pickup = hit.GetComponentInParent<PickupItem>();
                if (pickup == null)
                    continue;

                Vector3 flatDelta = pickup.transform.position - origin;
                flatDelta.y = 0f;
                float sqrDistance = flatDelta.sqrMagnitude;
                float pickupRadius = pickup.InteractionRadius;
                if (sqrDistance > pickupRadius * pickupRadius || sqrDistance >= nearestSqrDistance)
                    continue;

                nearest = pickup;
                nearestSqrDistance = sqrDistance;
            }

            return nearest;
        }
    }
}
