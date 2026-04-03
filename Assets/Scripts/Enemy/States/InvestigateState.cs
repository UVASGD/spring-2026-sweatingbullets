using UnityEngine;
using UnityEngine.AI;

namespace Enemy.States
{
    public class InvestigateState : EnemyStateBase
    {
        private Vector3 _targetPosition;
        private float _investigateTimer;
        private float _lookAroundTime = 3f;
        private float _moveSpeed = 3f;
        private bool _arrivedAtTarget;
        private float _rotateSpeed = 90f; // degrees per second for looking around

        public bool IsFinished { get; private set; }

        public InvestigateState(bool needsExitTime, EnemyAI enemy)
            : base(needsExitTime, enemy) { }

        public void SetTargetPosition(Vector3 position)
        {
            _targetPosition = position;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            IsFinished = false;
            _arrivedAtTarget = false;
            _investigateTimer = 0f;

            if (Agent != null)
            {
                Agent.isStopped = false;
                Agent.speed = _moveSpeed;
                Agent.SetDestination(_targetPosition);
            }
        }

        public override void OnLogic()
        {
            base.OnLogic();

            if (Agent == null || !Agent.isActiveAndEnabled) return;

            if (!_arrivedAtTarget)
            {
                // Still walking to the noise location
                if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance)
                {
                    _arrivedAtTarget = true;
                    Agent.isStopped = true;
                    _investigateTimer = 0f;
                }
            }
            else
            {
                // Look around at the location
                _investigateTimer += Time.deltaTime;
                Enemy.transform.Rotate(0f, _rotateSpeed * Time.deltaTime, 0f);

                if (_investigateTimer >= _lookAroundTime)
                {
                    IsFinished = true;
                }
            }
        }

        public override void OnExit()
        {
            base.OnExit();
            if (Agent != null)
                Agent.isStopped = false;
        }
    }
}
