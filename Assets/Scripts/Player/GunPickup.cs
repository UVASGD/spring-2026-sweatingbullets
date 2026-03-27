using UnityEngine;
using SUPERCharacter;

namespace Player
{
    public class GunPickup : MonoBehaviour, IInteractable
    {
        public bool Interact()
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                return false;
            }

            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController == null || playerController.HasGun)
            {
                return false;
            }

            playerController.GiveGun();
            Destroy(gameObject);
            return true;
        }
    }
}
