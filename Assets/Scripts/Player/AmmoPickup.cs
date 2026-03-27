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

        public bool Interact()
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject == null)
            {
                return false;
            }

            PlayerController playerController = playerObject.GetComponent<PlayerController>();
            if (playerController == null)
            {
                return false;
            }

            playerController.AddAmmo(ammoAmount);
            Destroy(gameObject);
            return true;
        }
    }
}
