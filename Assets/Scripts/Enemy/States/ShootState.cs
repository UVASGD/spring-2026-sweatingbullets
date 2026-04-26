using UnityEngine;
using UnityHFSM;
using System;

namespace Enemy.States
{
    public class ShootState : EnemyStateBase
    {
        private bool _isPlayerHit = false;
        private bool _hasFired;
        public bool IsDone { get; private set; } // flag
        private float _aimTime;
        private float _weaponRange;
        private EnemyShoot _shooter;

        public ShootState(bool needsExitTime, EnemyAI enemy, float aimTime, float weaponRange, float ExitTime = 0.33f)
            : base(needsExitTime, enemy, ExitTime)
        {
            _aimTime = aimTime;
            _weaponRange = weaponRange;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            if (Agent!= null) Agent.isStopped = true;
            _hasFired = false;
            IsDone = false;
            LookAtPlayer();
            _shooter = Enemy.weapon.GetComponent<EnemyShoot>();
            _shooter.Range = _weaponRange;
            _shooter.PlayCockingSound();
        }
        
        public override void OnLogic()
        {
            // continuously look at the player while shooting
            LookAtPlayer();
            //Debug.Log(timer.Elapsed + " " + RequestedExit);
            if (!_hasFired && timer.Elapsed >= _aimTime) // is aiming pause over?
            {
                _shooter.FireWeapon();
                _hasFired = true;
                // recoil goes here if I want to do it
                IsDone = true; // now transition!

            }
            if (_hasFired) base.OnLogic();
        }

        private void LookAtPlayer()
        {
            if (Enemy.player != null)
            {
                Vector3 dir = (Enemy.player.transform.position - Enemy.transform.position).normalized;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Enemy.transform.rotation = Quaternion.Slerp(Enemy.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
                }
            }
        }
        public override void OnExitRequest()
        {
            // Only allow the FSM to request an exit if we have finished firing.
            if (IsDone) base.OnExitRequest();
        }
    }
}