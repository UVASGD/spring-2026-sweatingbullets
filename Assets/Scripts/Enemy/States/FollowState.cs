using UnityEngine;

namespace Enemy.States
{
    public class FollowState: EnemyStateBase
    {
        private Transform _target;

        public FollowState(bool needsExitTime, EnemyAI enemy, Transform target) : base(needsExitTime, enemy)
        {
            _target = target;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            Agent.enabled = true;
            Agent.isStopped = false;
            Agent.speed = 5f;
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