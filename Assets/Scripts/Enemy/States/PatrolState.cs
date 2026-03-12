using System;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityHFSM;
using Random = UnityEngine.Random;
using UnityEngine.AI;

namespace Enemy.States
{
    public class PatrolState : EnemyStateBase
    {
        public PatrolState(
            bool needsExitTime,
            EnemyAI Enemy, 
            float patrolRadius, 
            float moveSpeed, 
            float waitTime,
            float timeLeft) : base(needsExitTime, Enemy)
        {
            _patrolRadius = patrolRadius;
            _moveSpeed = moveSpeed;
            _waitTime = waitTime;
            _timeLeft = timeLeft;
        }

        public bool IsDone {get; private set;}
        private Vector3 _targetPosition;
        private float _patrolRadius = 10f;
        private float _moveSpeed = 2f;
        private float _waitTimer;
        private float _waitTime = 2f;
        private float _timeLeft = 10000f;
        
        public PatrolState(bool needsExitTime, EnemyAI enemy) : base(needsExitTime, enemy) { }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Agent != null)
            {
                Agent.enabled = true;
                //Agent.isStopped = true;
                Agent.speed = 2f;
            }
            // randomly walk about
            PickNewTarget();
        }

        public override void OnLogic()
        {
            base.OnLogic();

            if (Agent == null || !Agent.isActiveAndEnabled) return;

            _timeLeft -= Time.deltaTime;
            if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance)
            {
                Debug.Log("Waiting for target");
                _waitTimer += Time.deltaTime;
                if (_waitTimer > _waitTime)
                {
                    PickNewTarget();
                    _waitTimer = 0f;
                }
            }

        }

        private void PickNewTarget()
        {
            Debug.Log("Picking target");
            // random point inside circle for target
            Vector2 randomCircle = Random.insideUnitCircle * _patrolRadius;
            _targetPosition = Enemy.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            Debug.Log(Enemy.transform.position + " " + _targetPosition);
            Agent.SetDestination(_targetPosition);
        }
    }
}