using UnityEngine;

namespace Player
{
    /// <summary>
    /// Base class for any input that can affect the player's nerves value.
    /// Each subclass represents one source of nerves (ADS, reload cancel, aim tapping, etc.)
    /// and is responsible for its own timing, accumulation, and cap logic.
    /// </summary>
    public abstract class NervesInput : MonoBehaviour
    {
        [SerializeField] private float maxContribution = 30f;

        private float _accumulatedNerves;

        /// <summary>
        /// The maximum amount of nerves this input can contribute.
        /// </summary>
        public float MaxContribution => maxContribution;

        /// <summary>
        /// How much nerves this input has contributed so far.
        /// </summary>
        public float AccumulatedNerves => _accumulatedNerves;

        /// <summary>
        /// Called every frame by NervesManager. Returns the nerves delta for this frame
        /// (positive = increase, 0 = no change). The base class handles capping.
        /// </summary>
        public float Evaluate()
        {
            float raw = CalculateNervesDelta();

            if (raw <= 0f)
                return 0f;

            float remaining = Mathf.Max(0f, maxContribution - _accumulatedNerves);
            float applied = Mathf.Min(raw, remaining);
            _accumulatedNerves += applied;
            return applied;
        }

        /// <summary>
        /// Called when overall nerves decrease. Reduces this input's accumulated amount
        /// proportionally so its cap frees back up over time.
        /// </summary>
        public void ReduceAccumulation(float amount)
        {
            _accumulatedNerves = Mathf.Max(0f, _accumulatedNerves - amount);
        }

        /// <summary>
        /// Subclasses implement this to return the raw nerves increase for this frame.
        /// Return 0 if this input is not currently active.
        /// </summary>
        protected abstract float CalculateNervesDelta();
    }
}
