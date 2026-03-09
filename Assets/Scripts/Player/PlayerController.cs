using UnityEngine;
using System.Collections;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private DeathCameraController deathCam;
        private Rigidbody _rb;
        private bool _isDead;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            _rb = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void Update()
        {
            
        }
        public void Hit(Vector3 hitPoint, Vector3 hitDirection)
        {
            if (_isDead) return;
            _isDead = true;
            Debug.Log("Omg! The player died. oof");
            if (deathCam) deathCam.ActivateDeathCam();
            StartCoroutine(DelayedFall(hitPoint, hitDirection));
        }
        IEnumerator DelayedFall(Vector3 hitPoint, Vector3 hitDirection)
        {
            _rb.isKinematic = false;
            _rb.useGravity = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;

            _rb.AddForceAtPosition(hitDirection * 10f, hitPoint, ForceMode.Impulse);

            yield return new WaitForSeconds(0.3f);

            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.None;
        }
    }
}