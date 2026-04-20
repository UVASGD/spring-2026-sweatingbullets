using UnityEngine;
using SUPERCharacter;

namespace Player
{
    public class GunPickup : MonoBehaviour, IInteractable
    {
        private int _bulletCount;

        public void Initialize(int bulletCount)
        {
            _bulletCount = bulletCount;
        }

        public bool CanInteract(PlayerController playerController)
        {
            return playerController != null && !playerController.HasGun;
        }

        public bool Interact(PlayerController playerController)
        {
            if (!CanInteract(playerController))
            {
                return false;
            }

            playerController.GiveGun(_bulletCount);
            Destroy(gameObject);
            return true;
        }
    }
}
