using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

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
        private readonly List<Vector3> _visitedPositions = new List<Vector3>();
        private float _avoidRadius = 8f;
        private int _maxHistory = 8;
        private int _maxPickAttempts = 10;

        public PatrolState(bool needsExitTime, EnemyAI enemy) : base(needsExitTime, enemy) { }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Agent != null)
            {
                Agent.enabled = true;
                Agent.speed = _moveSpeed;
            }
            PickNewTarget();
        }

        public override void OnLogic()
        {
            base.OnLogic();

            if (Agent == null || !Agent.isActiveAndEnabled) return;

            _timeLeft -= Time.deltaTime;
            if (!Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance)
            {
                _waitTimer += Time.deltaTime;
                if (_waitTimer > _waitTime)
                {
                    RecordVisited(Enemy.transform.position);
                    PickNewTarget();
                    _waitTimer = 0f;
                }
            }
        }

        private void PickNewTarget()
        {
            Vector3 bestCandidate = Enemy.transform.position;
            float bestMinDist = -1f;

            for (int i = 0; i < _maxPickAttempts; i++)
            {
                Vector2 randomCircle = Random.insideUnitCircle.normalized
                    * Random.Range(_avoidRadius, _patrolRadius);
                Vector3 candidate = Enemy.transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);

                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, _patrolRadius, NavMesh.AllAreas))
                    continue;

                float minDistToVisited = MinDistanceToVisited(hit.position);

                if (minDistToVisited > bestMinDist)
                {
                    bestMinDist = minDistToVisited;
                    bestCandidate = hit.position;
                }
            }

            Agent.SetDestination(bestCandidate);
        }

        private float MinDistanceToVisited(Vector3 position)
        {
            if (_visitedPositions.Count == 0)
                return float.MaxValue;

            float min = float.MaxValue;
            foreach (var visited in _visitedPositions)
            {
                float dist = Vector3.Distance(position, visited);
                if (dist < min)
                    min = dist;
            }
            return min;
        }

        private void RecordVisited(Vector3 position)
        {
            _visitedPositions.Add(position);
            if (_visitedPositions.Count > _maxHistory)
                _visitedPositions.RemoveAt(0);
        }
    }
}
