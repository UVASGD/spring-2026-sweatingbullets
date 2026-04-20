using UnityEngine;

namespace Player
{
    public class PassiveNervesInput : NervesInput
    {
        [SerializeField] private float nervesIncreasePerSecond = 2f;

        protected override float CalculateNervesDelta()
        {
            return nervesIncreasePerSecond * Time.deltaTime;
        }
    }
}
