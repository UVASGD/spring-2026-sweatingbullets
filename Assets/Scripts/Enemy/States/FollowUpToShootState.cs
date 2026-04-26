using UnityEngine;
using UnityHFSM;
using System;
using Unity.VisualScripting.FullSerializer;
using UnityEngine.AI;
using Random = UnityEngine.Random;


namespace Enemy.States
{
    public class FollowUpToShootState : EnemyStateBase
    {
        private Vector3 _strafeTarget;

        private float strafeDistance;
        private float _strafeSpeed;

        public FollowUpToShootState(bool needsExitTime, EnemyAI enemy, Transform player, float strafeDistance,
            float strafeSpeed = 2f, float ExitTime = 0.33f)
            : base(needsExitTime, enemy, ExitTime)
        {
            this.strafeDistance = strafeDistance;
            _strafeSpeed = strafeSpeed;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Agent != null)
            {
                Agent.enabled = true;
                Agent.isStopped = false;
                Agent.speed = _strafeSpeed;

                Agent.updateRotation = false;
                if (!RequestedExit && Agent != null) PickNewTarget();
            }
        }
        
        public override void OnLogic()
        {
            base.OnLogic();

            if (!RequestedExit && Agent != null)
            {
                RotateTowardsTarget(Enemy.player.transform.position);
                //Debug.Log(Vector3.Distance(Enemy.transform.position, _strafeTarget) + " " + Agent.stoppingDistance + "; " + !Agent.pathPending);
                // Check if we reached the strafe position
                if (Vector3.Distance(Enemy.transform.position, _strafeTarget) <= Agent.stoppingDistance + Agent.baseOffset && !Agent.pathPending)
                {
                    fsm.StateCanExit(); // transition back to Shoot
                }
            }
        }

        private void RotateTowardsTarget(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - Enemy.transform.position).normalized;
            direction.y = 0; // no tilting up or down
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion lookRotation = Quaternion.LookRotation(direction);
                Enemy.transform.rotation = Quaternion.Slerp(Enemy.transform.rotation, lookRotation, Time.deltaTime * 5f);
            }
        }

        private void PickNewTarget()
        {
            int leftOrRight = Random.Range(0, 2); // 0 or 1
            Vector3 dir = leftOrRight > 0 ? -Enemy.transform.right : Enemy.transform.right;
            _strafeTarget = Enemy.transform.position + dir * strafeDistance;
            
            // Avoid walking into obstacles, use navmesh to go
            if (NavMesh.SamplePosition(_strafeTarget, out NavMeshHit hit, strafeDistance, UnityEngine.AI.NavMesh.AllAreas))
            {
                _strafeTarget = hit.position;
            }
            else
            {
                _strafeTarget = Enemy.transform.position;
            }
            Agent.SetDestination(_strafeTarget);
        }
        
        public override void OnExit()
        {
            base.OnExit();
            // Re-enable auto-rotation for other states
            if (Agent != null) Agent.updateRotation = true;
        }

        public override void OnExitRequest()
        {
            // do nothing. prevents enemystatebase from setting requestedexit = true
        }
    }

}
