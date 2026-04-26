using UnityEngine;
using System.Collections;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private DeathCameraController deathCam;
        [SerializeField] private MonoBehaviour movementScript;
        [SerializeField] private Audio.BattleMusicController battleMusic;

        private Rigidbody _rb;
        private bool _isDead;

        void Start()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void Hit(Vector3 hitPoint, Vector3 hitDirection)
        {
            if (_isDead) return;
            _isDead = true;

            Debug.Log("Omg! The player died. oof");

            if (battleMusic != null)
                battleMusic.StopBattleMusic();

            // Disable player movement
            if (movementScript)
                movementScript.enabled = false;

            // Activate death camera
            if (deathCam)
                deathCam.ActivateDeathCam();

            StartCoroutine(DelayedFall(hitPoint, hitDirection));
        }

        IEnumerator DelayedFall(Vector3 hitPoint, Vector3 hitDirection)
        {
            // Clear existing movement
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // Enable physics
            _rb.isKinematic = false;
            _rb.useGravity = true;

            // Allow the capsule to rotate and fall
            _rb.constraints = RigidbodyConstraints.None;

            // Apply force where the player was hit
            _rb.AddForceAtPosition(hitDirection * 3f, hitPoint, ForceMode.Impulse);

            // Add a little torque so the capsule tips over
            _rb.AddTorque(transform.right * 1f, ForceMode.Impulse);

            yield return null;
        }
    }
}