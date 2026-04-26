using UnityEngine;

namespace Enemy.States
{
    public class FollowState: EnemyStateBase
    {
        private Transform _target;
        private float _moveSpeed;

        public FollowState(bool needsExitTime, EnemyAI enemy, Transform target, float moveSpeed = 5f) : base(needsExitTime, enemy)
        {
            _target = target;
            _moveSpeed = moveSpeed;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            Agent.enabled = true;
            Agent.isStopped = false;
            Agent.speed = _moveSpeed;
        }

        public override void OnLogic()
        {
            base.OnLogic();
            if (!RequestedExit)
            {
                Agent.SetDestination(_target.position);
            }
            else if (Agent.remainingDistance <= Agent.stoppingDistance)
            {
                fsm.StateCanExit();
            }
        }
    }
}