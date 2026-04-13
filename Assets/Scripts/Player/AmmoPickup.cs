using UnityEngine;
using SUPERCharacter;

namespace Player
{
    public class AmmoPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private int ammoAmount = 1;

        public void SetAmmoAmount(int amount)
        {
            ammoAmount = Mathf.Max(1, amount);
        }

        public bool CanInteract(PlayerController playerController)
        {
            return playerController != null;
        }

        public bool Interact(PlayerController playerController)
        {
            if (!CanInteract(playerController))
            {
                return false;
            }

            playerController.AddAmmo(ammoAmount);
            Destroy(gameObject);
            return true;
        }
    }
}
