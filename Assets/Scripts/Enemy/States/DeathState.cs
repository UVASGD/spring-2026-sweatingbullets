using UnityEngine;
using UnityHFSM;
using System.Collections;

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
            Enemy.StartCoroutine(DelayedWin());
        }
        private IEnumerator DelayedWin()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            GameManager.Instance.ShowWinScreen();
        }
    }
}