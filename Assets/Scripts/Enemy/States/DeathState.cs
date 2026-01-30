using UnityEngine;
using UnityHFSM;

namespace Enemy.States {
    public class DeathState : EnemyStateBase
    {
        public DeathState(EnemyAI enemy)
            : base(false, enemy)
        {
        }

        public override void OnEnter()
        {
            Agent.enabled = false;
            
            Enemy.isDead = true;
        }
    }
}