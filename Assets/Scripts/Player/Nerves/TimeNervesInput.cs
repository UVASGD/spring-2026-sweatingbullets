using UnityEngine;

namespace Player
{
    /// <summary>
    /// Implicit time pressure: establishes an escalating floor on the nerves
    /// value that recovery cannot push below. Floor grows over time. (Difficulty
    /// scaling deprecated — base rate only.) Does not contribute through the normal
    /// delta channel — recovery would otherwise overwhelm the slow rate.
    /// </summary>
    public class TimeNervesInput : NervesInput
    {
        [Header("Time Pressure Settings")]
        [Tooltip("Seconds before time pressure starts establishing a floor.")]
        [SerializeField] private float gracePeriodSeconds = 15f;

        [Tooltip("Base floor growth per second, just past the grace period.")]
        [SerializeField] private float baseRatePerSecond = 0.4f;

        [Tooltip("Rate = base * (1 + secondsAfterGrace / rampSeconds), capped at maxTimeMultiplier.")]
        [SerializeField] private float rampSeconds = 60f;

        [Tooltip("Plateau on the time-elapsed multiplier so long rounds don't spiral unbounded.")]
        [SerializeField] private float maxTimeMultiplier = 4f;

        // Difficulty deprecated.
        // [Tooltip("Per-difficulty multiplier added on top of base rate. d=1 -> 1x, d=10 -> 1 + 9 * boost.")]
        // [SerializeField] private float perDifficultyRateBoost = 0.25f;

        [Tooltip("Maximum floor this input can establish. Caps how high time alone can push the bar so the player can still feel relief from spikes.")]
        [SerializeField] private float maxFloor = 70f;

        private float _floor;

        public override float FloorContribution => _floor;

        private void Reset()
        {
            maxContribution = 60f;
        }

        private void OnEnable()
        {
            _floor = 0f;
        }

        private void Update()
        {
            if (GameManager.IsGameOver) return;

            float elapsed = GameManager.RoundElapsedSeconds;
            float secondsAfterGrace = elapsed - gracePeriodSeconds;
            if (secondsAfterGrace <= 0f) return;

            float timeMult = Mathf.Min(maxTimeMultiplier, 1f + secondsAfterGrace / Mathf.Max(0.01f, rampSeconds));
            // Difficulty deprecated.
            // float diffMult = 1f + perDifficultyRateBoost * (GameManager.Difficulty - 1);
            float ratePerSecond = baseRatePerSecond * timeMult; // was: * diffMult
            _floor = Mathf.Min(maxFloor, _floor + ratePerSecond * Time.deltaTime);
        }

        protected override float CalculateNervesDelta()
        {
            return 0f;
        }
    }
}
