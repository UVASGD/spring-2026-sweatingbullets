using UnityEngine;

namespace Player
{
    public class PickupItem : MonoBehaviour
    {
        public enum PickupType
        {
            Gun,
            Bullets
        }

        [SerializeField] private PickupType pickupType;
        [SerializeField] private int ammoAmount = 6;
        [SerializeField] private float interactionRadius = 2f;
        [SerializeField] private AudioClip pickupSound;

        public PickupType Type => pickupType;
        public int AmmoAmount => Mathf.Max(0, ammoAmount);
        public float InteractionRadius => Mathf.Max(0.1f, interactionRadius);

        public void Configure(PickupType type, int amount)
        {
            pickupType = type;
            ammoAmount = Mathf.Max(0, amount);
        }

        public bool TryPickup(WeaponController weaponController, AudioSource audioSource = null)
        {
            if (weaponController == null)
                return false;

            switch (pickupType)
            {
                case PickupType.Gun:
                    weaponController.SetHasWeapon(true);
                    break;
                case PickupType.Bullets:
                    weaponController.AddAmmo(AmmoAmount);
                    break;
                default:
                    return false;
            }

            if (pickupSound != null)
            {
                if (audioSource != null)
                    audioSource.PlayOneShot(pickupSound);
                else
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            Destroy(gameObject);
            return true;
        }
    }
}
