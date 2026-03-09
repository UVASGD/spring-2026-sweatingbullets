using Enemy.States;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using UnityHFSM;

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
        [SerializeField, Range(1f, 10f)] private float difficulty;
        [SerializeField, Range(1f, 10f)] private float strafeDistance;
        [SerializeField, Range(0f, 2f)] private float aimTime;
        [SerializeField, Range(5f, 100f)] private float weaponRange;

        [Header("Detection")]
        [SerializeField] private float detectionDistance = 100f;
        [SerializeField, Range(0, 360)] private float viewAngle = 120f;

        public bool isDead;
        
        public void Init(GameObject p)
        {
            player = p;
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
            _stateMachine.AddState(EnemyState.Patrol, new PatrolState(false, this));
            _stateMachine.AddState(EnemyState.Follow, new FollowState(false, this, player.transform));
            _stateMachine.AddState(EnemyState.Shoot, new ShootState(true, this, aimTime, weaponRange));
            _stateMachine.AddState(EnemyState.FollowUpToShoot, new FollowUpToShootState(true, this, player.transform, strafeDistance,5.0f));
            _stateMachine.AddState(EnemyState.Dead, new DeathState(this));

            // --- Condition-based transitions ---
            _stateMachine.AddTransition(new Transition<EnemyState>(EnemyState.Patrol, EnemyState.Follow,
                (transition) => CanSeePlayer())
            );

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
            // Additional death logic could go here
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

            if (Physics.Raycast(eyes.transform.position, direction.normalized, out RaycastHit hit, distance))
            {
                //print(hit.collider.gameObject.name);
                if (hit.collider.gameObject != player)
                {
                    return false;
                }
            }

            return true;
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
