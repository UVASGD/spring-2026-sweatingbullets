using UnityEngine;
using SUPERCharacter;

namespace Player
{
    public class GunPickup : MonoBehaviour, IInteractable
    {
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

            playerController.GiveGun();
            Destroy(gameObject);
            return true;
        }
    }
}
