using Enemy.States;
using Player;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityHFSM;
using System.Collections;
using System.Collections.Generic;

namespace Enemy
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyAI : MonoBehaviour
    {
        private StateMachine<EnemyState, StateEvent> _stateMachine;

        [Header("References")]
        public GameObject weapon;
        public GameObject player;
        public GameObject eyes;

        private NavMeshAgent _agent;
        private Rigidbody _rb;

        [Header("Enemy Stats")]
        [SerializeField, Range(1f, 10f)] private float strafeDistance;
        [SerializeField, Range(0f, 2f)] private float aimTime;
        [SerializeField, Range(5f, 100f)] private float weaponRange;

        [Header("Detection")]
        [SerializeField] private float detectionDistance = 100f;
        [SerializeField, Range(0, 360)] private float viewAngle = 120f;
        [SerializeField] private List<Vector3> viewOffsets;

        [Header("Hearing")]
        [SerializeField] private float hearingRange = 20f;
        [SerializeField] private float gunshotHearingRange = 50f;

        public bool isDead;

        public bool IsAwareOfPlayer
        {
            get
            {
                if (_stateMachine == null || isDead) return false;
                var state = _stateMachine.ActiveStateName;
                return state == EnemyState.Follow
                    || state == EnemyState.Shoot
                    || state == EnemyState.FollowUpToShoot;
            }
        }

        private PlayerNoiseEmitter _noiseEmitter;
        private Vector3 _lastHeardPosition;
        private InvestigateState _investigateState;
        
        public void Init(GameObject p)
        {
            player = p;
            _noiseEmitter = player.GetComponent<PlayerNoiseEmitter>();
            var shoot = weapon.GetComponent<Enemy.EnemyShoot>();
            if (shoot != null)
            {
                shoot.Range = weaponRange;
                shoot.SetPlayer(player.transform);
            }
            _rb = GetComponent<Rigidbody>();
            _stateMachine = new StateMachine<EnemyState, StateEvent>();
            _agent = GetComponent<NavMeshAgent>();

            // Add states
            _stateMachine.AddState(EnemyState.Patrol, new PatrolState(false, this, 10, 2, 2,100000));
            _stateMachine.AddState(EnemyState.Alert, new PatrolState(false, this, 10, 3.0f, 0.5f, 5));
            _investigateState = new InvestigateState(false, this);
            _stateMachine.AddState(EnemyState.Follow, new FollowState(false, this, player.transform));
            _stateMachine.AddState(EnemyState.Shoot, new ShootState(true, this, aimTime, weaponRange));
            _stateMachine.AddState(EnemyState.FollowUpToShoot, new FollowUpToShootState(true, this, player.transform, strafeDistance,5.0f));
            _stateMachine.AddState(EnemyState.Investigate, _investigateState);
            _stateMachine.AddState(EnemyState.Dead, new DeathState(this));

            // --- Condition-based transitions ---

            // Patrol: see player → Follow, hear player → Investigate
            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Patrol, EnemyState.Follow,
                (transition) => CanSeePlayer())
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Patrol, EnemyState.Investigate,
                (transition) => CanHearPlayer() && !CanSeePlayer(),
                onTransition: (transition) => _investigateState.SetTargetPosition(_lastHeardPosition))
            );

            // Investigate: see player → Follow, finished → Patrol, hear new noise → restart investigate
            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Investigate, EnemyState.Follow,
                (transition) => CanSeePlayer())
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Investigate, EnemyState.Patrol,
                (transition) => _investigateState.IsFinished)
            );

            // Follow and combat transitions (unchanged)
            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Follow, EnemyState.Shoot,
                (transition) => PlayerInRange() && CanSeePlayer())
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Shoot, EnemyState.FollowUpToShoot,
                (transition) => {
                    // Only transition if the shoot state itself says it is finished
                    var shootState = _stateMachine.GetState(EnemyState.Shoot) as ShootState;
                    return shootState != null && shootState.IsDone && PlayerInRange() && CanSeePlayer();
                })
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.FollowUpToShoot, EnemyState.Shoot,
                (transition) => PlayerInRange() && CanSeePlayer())
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.FollowUpToShoot, EnemyState.Follow,
                (transition) => !PlayerInRange() && CanSeePlayer()
            ));

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Follow, EnemyState.Patrol,
                (transition) => !CanSeePlayer())
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Shoot, EnemyState.Patrol,
                (transition) => !CanSeePlayer())
            );

            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Shoot, EnemyState.Follow,
                (transition) => !PlayerInRange())
            );

            // --- Trigger-based transitions (instant events) ---
            _stateMachine.AddTriggerTransition(StateEvent.IsPlayerDead,
                new Transition<EnemyState>(EnemyState.Shoot, EnemyState.Patrol));

            _stateMachine.AddTriggerTransition(StateEvent.Died,
                new Transition<EnemyState>(EnemyState.Patrol, EnemyState.Dead, forceInstantly: true));

            _stateMachine.AddTriggerTransition(StateEvent.Died,
                new Transition<EnemyState>(EnemyState.Follow, EnemyState.Dead, forceInstantly: true));

            _stateMachine.AddTriggerTransition(StateEvent.Died,
                new Transition<EnemyState>(EnemyState.Shoot, EnemyState.Dead, forceInstantly: true));

            _stateMachine.AddTriggerTransition(StateEvent.Died,
                new Transition<EnemyState>(EnemyState.FollowUpToShoot, EnemyState.Dead, forceInstantly: true));

            _stateMachine.AddTriggerTransition(StateEvent.Died,
                new Transition<EnemyState>(EnemyState.Investigate, EnemyState.Dead, forceInstantly: true));


            _stateMachine.SetStartState(EnemyState.Patrol);
            _stateMachine.Init();
            isDead = false;
        }

        void Update()
        {
            if (!isDead)
            {
                //CurrentState = _stateMachine != null ? _stateMachine.ActiveStateName.ToString() : "None";
                _stateMachine.OnLogic();
                //currstate = _stateMachine.ActiveState.ToString();
                // Handle "ragdoll falling" if rigidbody is non-kinematic
                // if (_rb.isKinematic == false)
                // {
                //     if (!Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 2f))
                //     {
                //         transform.position += Vector3.down * 3f * Time.deltaTime;
                //     }
                // }
            }
        }

        public void Hit(Vector3 hitPoint, Vector3 hitDirection)
        {
            // Apply physics hit
            _rb.isKinematic = false; 
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.AddForceAtPosition(hitDirection * 10f, hitPoint, ForceMode.Impulse);
            //_rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);

            if (isDead) return; // Already dead

            _stateMachine.Trigger(StateEvent.Died);
            Die();
        }

        // Exploding barrels disabled — ExplosionHit commented out.
        /*
        public void ExplosionHit(Vector3 hitDirection)
        {
            // Apply physics hit
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.None;
            _rb.AddForce(hitDirection * 15f, ForceMode.Impulse);
            //_rb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);

            if (isDead) return; // Already dead

            _stateMachine.Trigger(StateEvent.Died);
            Die();
        }
        */

        private void Die(){
            isDead = true;
            // Disable NavMeshAgent and other components as needed
            _agent.enabled = false;
            weapon.SetActive(false);
            // Additional death logic (e.g., play animation, drop loot) could go here
        }


        private bool CheckDir(Vector3 position, Vector3 dir, float distance)
        {
            if (Physics.Raycast(position, dir.normalized, out RaycastHit hit, distance))
            {
                Debug.DrawLine(position, hit.point, Color.green);
                //print(hit.collider.gameObject.name);
                return (hit.collider.gameObject == player);
            }
            Debug.DrawLine(position, dir.normalized * distance, Color.green);
            return false;
        }

        private bool CanSeePlayer()
        {
            if (player == null) return false;

            Vector3 direction = player.transform.position - eyes.transform.position;
            float distance = direction.magnitude;

            if (distance > detectionDistance)
            {
                return false;
            }

            float angle = Vector3.Angle(transform.forward, direction);
            if (angle > viewAngle / 2f)
            {
                //print(angle + ", " + viewAngle);

                return false;
            }

        
            
            if (CheckDir(eyes.transform.position, direction, distance))
            {
                return true;
            }

            direction.y = 0;
            if (CheckDir(eyes.transform.position, direction, distance))
            {
                return true;
            }

            foreach (Vector3 offset in viewOffsets)
            {
                if (CheckDir(eyes.transform.position + offset, direction, distance))
                {
                    return true;
                }
            }
            return false;
        }

        private bool CanHearPlayer()
        {
            if (player == null || _noiseEmitter == null) return false;

            float noise = _noiseEmitter.NoiseLevel;
            if (noise <= 0f) return false;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            float effectiveRange = _noiseEmitter.LastNoiseType == NoiseType.Gunshot
                ? gunshotHearingRange
                : hearingRange;

            // Scale range by noise intensity
            if (distance <= effectiveRange * noise)
            {
                _lastHeardPosition = player.transform.position;
                return true;
            }

            return false;
        }

        private bool PlayerInRange()
        {
            if (player == null) return false;
            float distance = Vector3.Distance(transform.position, player.transform.position);
            return distance <= weaponRange;
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_stateMachine != null && _stateMachine.ActiveStateName != null)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, 
                    $"State: {_stateMachine.ActiveStateName}");
            }
        }
        #endif
    }
}
