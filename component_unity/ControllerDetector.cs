using UnityEngine;
using UnityEngine.InputSystem;

using System;
using System.IO;
using System.Collections.Generic;
using Unity.InferenceEngine;

public class ControllerDetector : MonoBehaviour
{
    [SerializeField] string evaluationSavingDirectory = "TargetDirectory";

    [Header("ONNX Model")]
    public Unity.InferenceEngine.ModelAsset onnxModel;

    private Unity.InferenceEngine.Worker worker;

    private const int SequenceLength = 60;

    private Queue<Vector4> inputQueue = new Queue<Vector4>();

    public float CurrentGamepadProbability { get; private set; }

        // 로그 시스템
    
    [Serializable]
    public class ControllerEvalEntry
    {
        public float timestamp;
        public string dominantController;

        public float keyboardMouseProbability;
        public float gamepadProbability;
    }

    [Serializable]
    public class ControllerEvalLog
    {
        public List<ControllerEvalEntry> entries =
            new List<ControllerEvalEntry>();
    }

    private ControllerEvalLog runtimeLog =
        new ControllerEvalLog();

    private string lastDominantController = "";

    [Header("Input Actions")]
    [SerializeField]
    public PlayerInput playerInput;

    public InputAction moveAction;
    public InputAction lookAction;

    // History Buffers
    private Queue<Vector2> lookHistory =
        new Queue<Vector2>();

    private Queue<float> magnitudeHistory =
        new Queue<float>();

    private const int FeatureWindowSize = 12;

    // For saving 'jerk'
    private Vector2 lastAim;
    private Vector2 lastAimDelta;

    // Hint for comparing real device
    private float controlSchemeHint = 0.5f;

    
    private void Start()
    {
        var runtimeModel =
            ModelLoader.Load(onnxModel);

        worker =
            new Worker(
                runtimeModel,
                BackendType.GPUCompute
            );

        // Input Actions
        if (playerInput == null)
        {
            playerInput =
                GetComponent<PlayerInput>();
        }

        moveAction =
            playerInput.actions["Move"];

        lookAction =
            playerInput.actions["Look"];

        if (moveAction == null ||
            lookAction == null)
        {
            Debug.LogError(
                "Move/Look actions not found."
            );
        }
    }

    private float inferenceTimer;

    private const float InferenceInterval =
        0.25f;

    private void Update()
    {
        if (moveAction == null ||
            lookAction == null)
        {
            return;
        }

        // Refer Input System
        Vector2 move =
            moveAction.ReadValue<Vector2>();

        Vector2 look =
            lookAction.ReadValue<Vector2>();

        // Actual control scheme hint
        string currentScheme =
            playerInput.currentControlScheme;

        if (currentScheme.Contains("Gamepad"))
        {
            controlSchemeHint = 1.0f;
        }
        else if (
            currentScheme.Contains("Keyboard") ||
            currentScheme.Contains("Mouse")
        )
        {
            controlSchemeHint = 0.0f;
        }
        else
        {
            controlSchemeHint = 0.5f;
        }

        // Get characteristic
        Vector4 feature =
            ExtractFeature(move, look);

        inputQueue.Enqueue(feature);

        while (inputQueue.Count >
            SequenceLength)
        {
            inputQueue.Dequeue();
        }

        // Limit inference cycles 
        inferenceTimer += Time.deltaTime;

        if (
            inputQueue.Count == SequenceLength &&
            inferenceTimer >=
            InferenceInterval
        )
        {
            inferenceTimer = 0f;

            RunInference();
        }
    }

    // Get characteristic
    private Vector4 ExtractFeature(Vector2 move, Vector2 aim)
    {
        // Manage history
        lookHistory.Enqueue(aim);

        magnitudeHistory.Enqueue(
            aim.magnitude
        );

        while (
            lookHistory.Count >
            FeatureWindowSize
        )
        {
            lookHistory.Dequeue();
        }

        while (
            magnitudeHistory.Count >
            FeatureWindowSize
        )
        {
            magnitudeHistory.Dequeue();
        }

        // Check Move Digital
        float moveDigital =
            (
                Mathf.Approximately(move.x, 0f) ||
                Mathf.Approximately(
                    Mathf.Abs(move.x), 1f)
            ) &&
            (
                Mathf.Approximately(move.y, 0f) ||
                Mathf.Approximately(
                    Mathf.Abs(move.y), 1f)
            )
            ? 1f : 0f;

        // Calculate actual variance
        float variance = 0f;

        if (magnitudeHistory.Count > 1)
        {
            float mean = 0f;

            foreach (float m in magnitudeHistory)
            {
                mean += m;
            }

            mean /= magnitudeHistory.Count;

            foreach (float m in magnitudeHistory)
            {
                float diff = m - mean;

                variance += diff * diff;
            }

            variance /= magnitudeHistory.Count;
        }

        // Max Speed
        float maxSpeed = 0f;

        foreach (float m in magnitudeHistory)
        {
            if (m > maxSpeed)
            {
                maxSpeed = m;
            }
        }

        // Calculate Jerk
        Vector2 currentDelta =
            aim - lastAim;

        Vector2 jerkVector =
            currentDelta - lastAimDelta;

        float jerk =
            jerkVector.magnitude;

        lastAim = aim;
        lastAimDelta = currentDelta;

        // Zero Crossing
        float zeroCross = 0f;

        if (lookHistory.Count > 1)
        {
            Vector2[] arr =
                lookHistory.ToArray();

            int crossings = 0;

            for (int i = 1; i < arr.Length; i++)
            {
                if (
                    Mathf.Sign(arr[i - 1].x) !=
                    Mathf.Sign(arr[i].x)
                )
                {
                    crossings++;
                }

                if (
                    Mathf.Sign(arr[i - 1].y) !=
                    Mathf.Sign(arr[i].y)
                )
                {
                    crossings++;
                }
            }

            zeroCross =
                (float)crossings /
                (arr.Length - 1);
        }

        // Hybrid Bias
        // Apply real device hint on variance

        variance +=
            controlSchemeHint * 0.02f;

        return new Vector4(
            moveDigital,
            variance + jerk,
            maxSpeed,
            zeroCross
        );
    }

    // Inferencing
    private void RunInference()
    {
        float[] data =
            new float[SequenceLength * 4];

        int idx = 0;

        foreach (var f in inputQueue)
        {
            data[idx++] = f.x;
            data[idx++] = f.y;
            data[idx++] = f.z;
            data[idx++] = f.w;
        }

        Tensor<float> inputTensor = null;
        Tensor<float> outputTensor = null;
        Tensor<float> cpuTensor = null;

        try
        {
            inputTensor =
                new Tensor<float>(
                    new TensorShape(1, SequenceLength, 4),
                    data
                );

            worker.Schedule(inputTensor);

            outputTensor =
                worker.PeekOutput() as Tensor<float>;

            cpuTensor =
                outputTensor.ReadbackAndClone();

            // Get output
            CurrentGamepadProbability =
                cpuTensor[0, 0];

            // Prevent NaN
            if (float.IsNaN(CurrentGamepadProbability) ||
                float.IsInfinity(CurrentGamepadProbability))
            {
                Debug.LogWarning(
                    "Invalid inference output."
                );

                return;
            }

            float gamepadProb =
                Mathf.Clamp01(
                    CurrentGamepadProbability
                );

            float keyboardProb =
                1.0f - gamepadProb;

            // Set dominantController
            string dominantController;

            if (gamepadProb >= 0.7f)
            {
                dominantController = "Gamepad";
            }
            else if (gamepadProb <= 0.3f)
            {
                dominantController =
                    "Keyboard&Mouse";
            }
            else
            {
                dominantController = "Uncertain";
            }

            // Only if dominant is changed, record
            if (
                lastDominantController == null ||
                dominantController !=
                lastDominantController
            )
            {
                ControllerEvalEntry entry =
                    new ControllerEvalEntry();

                entry.timestamp =
                    (float)Math.Round(Time.time, 4);

                entry.dominantController =
                    dominantController;

                entry.keyboardMouseProbability =
                    (float)Math.Round(keyboardProb, 4);

                entry.gamepadProbability =
                    (float)Math.Round(gamepadProb, 4);

                runtimeLog.entries.Add(entry);

                lastDominantController =
                    dominantController;

                Debug.Log(
                    $"[LOGGED] {dominantController}"
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"Inference Error:\n{e}"
            );
        }
        finally
        {
            inputTensor?.Dispose();
            outputTensor?.Dispose();
            cpuTensor?.Dispose();
        }
    }

    // Save on runtime
    private void OnApplicationQuit()
    {
        SaveRuntimeLog();

        worker?.Dispose();
    }

    // Save as JSON
    private void SaveRuntimeLog()
    {
        try
        {
            string documentsPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments
                );

            string directory =
                Path.Combine(
                    documentsPath,
                    evaluationSavingDirectory
                );

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string filename =
                $"controleval_" +
                $"{DateTime.Now:yyyyMMdd_HHmmss}.json";

            string fullPath =
                Path.Combine(directory, filename);

            string json =
                JsonUtility.ToJson(
                    runtimeLog,
                    true
                );

            File.WriteAllText(fullPath, json);

            Debug.Log(
                $"Controller log saved:\n{fullPath}"
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"Failed to save controller log:\n{e}"
            );
        }
    }

    private void OnDestroy()
    {
        worker?.Dispose();
    }
}