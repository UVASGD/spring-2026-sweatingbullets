using UnityEngine;

namespace Player
{
    /// <summary>
    /// Central orchestrator for the nerves system. Discovers all NervesInput sources
    /// and the NervesRecovery component, evaluates them each frame, and pushes the
    /// resulting nerves level to the visual and audio controllers.
    /// </summary>
    public class NervesManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NervesVisualEffectsController visualController;
        [SerializeField] private NervesAudioController audioController;

        [Header("Debug (Read Only)")]
        [SerializeField] private float currentNerves;
        [SerializeField] private float totalInputsDelta;
        [SerializeField] private float recoveryDelta;
        [SerializeField] private bool showDebugInfo = true;

        private NervesInput[] _inputs;
        private NervesRecovery _recovery;

        private void Awake()
        {
            _inputs = GetComponentsInChildren<NervesInput>();
            _recovery = GetComponentInChildren<NervesRecovery>();

            if (visualController == null)
                visualController = GetComponentInChildren<NervesVisualEffectsController>();

            if (audioController == null)
                audioController = GetComponentInChildren<NervesAudioController>();
        }

        private void Update()
        {
            ProcessInputs();
            ProcessRecovery();
            PushNervesToControllers();
        }

        private void ProcessInputs()
        {
            totalInputsDelta = 0f;
            for (int i = 0; i < _inputs.Length; i++)
            {
                float delta = _inputs[i].Evaluate();
                if (delta > 0f)
                {
                    totalInputsDelta += delta;
                    currentNerves = Mathf.Clamp(currentNerves + delta, 0f, 100f);
                }
            }
        }

        private void ProcessRecovery()
        {
            if (_recovery == null)
            {
                recoveryDelta = 0f;
                return;
            }

            float decrease = _recovery.Evaluate();
            recoveryDelta = decrease;

            if (decrease <= 0f)
                return;

            float before = currentNerves;
            currentNerves = Mathf.Clamp(currentNerves - decrease, 0f, 100f);

            float actualDecrease = before - currentNerves;
            if (actualDecrease > 0f)
                DistributeAccumulationReduction(actualDecrease);
        }

        private void DistributeAccumulationReduction(float totalDecrease)
        {
            float totalAccumulated = 0f;
            for (int i = 0; i < _inputs.Length; i++)
                totalAccumulated += _inputs[i].AccumulatedNerves;

            if (totalAccumulated <= 0f)
                return;

            for (int i = 0; i < _inputs.Length; i++)
            {
                float proportion = _inputs[i].AccumulatedNerves / totalAccumulated;
                _inputs[i].ReduceAccumulation(totalDecrease * proportion);
            }
        }

        private void PushNervesToControllers()
        {
            if (visualController != null)
                visualController.SetNervesLevel(currentNerves);

            if (audioController != null)
                audioController.SetNervesLevel(currentNerves);
        }

        private void OnGUI()
        {
            if (!showDebugInfo)
                return;

            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 200));
            GUILayout.Label("Nerves System Debug", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);

            GUILayout.Label($"Current Nerves: {currentNerves:F1}");
            GUILayout.Label($"Inputs Delta: {totalInputsDelta:F2}");
            GUILayout.Label($"Recovery Delta: {recoveryDelta:F2}");
            GUILayout.Space(5);

            GUILayout.Label("Inputs:");
            for (int i = 0; i < _inputs.Length; i++)
            {
                var input = _inputs[i];
                GUILayout.Label($"  {input.GetType().Name}: {input.AccumulatedNerves:F1}/{input.MaxContribution}");
            }

            if (GUILayout.Button("Reset Nerves"))
            {
                currentNerves = 0f;
                for (int i = 0; i < _inputs.Length; i++)
                {
                    // Reset accumulation via reflection since we don't have a public method
                    var field = typeof(NervesInput).GetField("_accumulatedNerves", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    field?.SetValue(_inputs[i], 0f);
                }
            }

            GUILayout.EndArea();
        }
    }
}
