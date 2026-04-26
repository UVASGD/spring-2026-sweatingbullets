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
        private const float MinNerves = 0f;
        private const float MaxNerves = 100f;

        [Header("References")]
        [SerializeField] private NervesVisualEffectsController visualController;
        [SerializeField] private NervesAudioController audioController;

        [Header("Debug (Read Only)")]
        public float CurrentNerves { get; private set; }
        [SerializeField] private float totalInputsDelta;
        [SerializeField] private float recoveryDelta;
        [SerializeField] private float currentFloor;
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

            if (_recovery == null)
                Debug.LogError("NervesManager: Could not find NervesRecovery in children — recovery will never run!");
        }

        private void Update()
        {
            if (GameManager.IsGameOver) return;

            currentFloor = ComputeFloor();
            if (CurrentNerves < currentFloor)
                CurrentNerves = currentFloor;

            ProcessInputs();
            ProcessRecovery();
            PushNervesToControllers();
        }

        private float ComputeFloor()
        {
            float floor = 0f;
            for (int i = 0; i < _inputs.Length; i++)
                floor += _inputs[i].FloorContribution;
            return Mathf.Clamp(floor, MinNerves, MaxNerves);
        }

        private void ProcessInputs()
        {
            totalInputsDelta = 0f;

            for (int i = 0; i < _inputs.Length; i++)
            {
                float delta = _inputs[i].Evaluate();
                if (delta <= 0f)
                    continue;

                totalInputsDelta += delta;
                AddNerves(delta);
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

            float actualDecrease = RemoveNerves(decrease);
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

        private void AddNerves(float amount)
        {
            CurrentNerves = Mathf.Clamp(CurrentNerves + amount, MinNerves, MaxNerves);
        }

        private float RemoveNerves(float amount)
        {
            float before = CurrentNerves;
            float lowerBound = Mathf.Max(MinNerves, currentFloor);
            CurrentNerves = Mathf.Clamp(CurrentNerves - amount, lowerBound, MaxNerves);
            return before - CurrentNerves;
        }

        private void ResetAllNervesState()
        {
            CurrentNerves = 0f;
            totalInputsDelta = 0f;
            recoveryDelta = 0f;

            for (int i = 0; i < _inputs.Length; i++)
                _inputs[i].ResetAccumulation();
        }

        private void PushNervesToControllers()
        {
            if (visualController != null)
                visualController.SetNervesLevel(CurrentNerves);

            if (audioController != null)
                audioController.SetNervesLevel(CurrentNerves);
        }

        private void OnGUI()
        {
            if (!showDebugInfo)
                return;

            GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 200));
            GUILayout.Label("Nerves System Debug", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);

            GUILayout.Label($"Current Nerves: {CurrentNerves:F1}");
            GUILayout.Label($"Floor: {currentFloor:F1}");
            GUILayout.Label($"Inputs Delta: {totalInputsDelta:F2}");
            GUILayout.Label($"Recovery Delta: {recoveryDelta:F2}");
            GUILayout.Label($"Round Time: {GameManager.RoundElapsedSeconds:F1}s  Difficulty: {GameManager.Difficulty}");
            GUILayout.Space(5);

            GUILayout.Label("Inputs:");
            for (int i = 0; i < _inputs.Length; i++)
            {
                var input = _inputs[i];
                GUILayout.Label($"  {input.GetType().Name}: {input.AccumulatedNerves:F1}/{input.MaxContribution}");
            }

            if (GUILayout.Button("Reset Nerves"))
                ResetAllNervesState();

            GUILayout.EndArea();
        }
    }
}
