using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputDataLogger : MonoBehaviour
{
    [SerializeField] string logSavingDirectory = "TargetDirectory";

    [Header("Input Action Names")]
    public string moveActionName = "Move";
    public string aimActionName = "Look";

    [Header("Log Settings")]
    public float saveInterval = 15f;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction aimAction;

    // Input of last frame (for delta)
    private Vector2 lastMoveInput;
    private Vector2 lastAimInput;

    // Struct for making snapshots of control
    public struct InputLogRecord
    {
        public float timeSinceLevelLoad;
        public Vector2 moveDelta;
        public Vector2 aimDelta;
        public string controlScheme;
    }

    private List<InputLogRecord> logRecords = new List<InputLogRecord>();
    private float timer = 0f;

    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        moveAction = playerInput.actions[moveActionName];
        aimAction = playerInput.actions[aimActionName];

        if (moveAction == null || aimAction == null)
        {
            Debug.LogError("InputDataLogger: Cannot find the input which is defined in inspector.");
        }
    }

    void FixedUpdate()
    {
        if (moveAction == null || aimAction == null) return;

        Vector2 currentMoveInput = moveAction.ReadValue<Vector2>();
        Vector2 currentAimInput = aimAction.ReadValue<Vector2>();

        Vector2 moveDelta = currentMoveInput - lastMoveInput;
        Vector2 aimDelta = currentAimInput - lastAimInput;

        // If is there an control
        if(IsControlInputUpdated(moveDelta, aimDelta))
        {
            logRecords.Add(new InputLogRecord
            {
                timeSinceLevelLoad = Time.timeSinceLevelLoad,
                moveDelta = moveDelta,
                aimDelta = aimDelta,
                controlScheme = playerInput.currentControlScheme
            });
        }

        // Save the control status for next frame comparison
        lastMoveInput = currentMoveInput;
        lastAimInput = currentAimInput;

        // Check the timer and save control log as file
        timer += Time.deltaTime;
        if (timer >= saveInterval)
        {
            SaveDataToFile();
            timer = 0f;
        }
    }

    bool IsControlInputUpdated(Vector2 md, Vector2 ld)
    {
        return (
            md.x != 0 || md.y != 0 || ld.x != 0 || ld.y != 0
        );
    }

    private void SaveDataToFile()
    {
        if (logRecords.Count == 0) return;

        // Set directory as "My document/charm_controllog"
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string directoryPath = Path.Combine(documentsPath, logSavingDirectory);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Set name: InputLog_YYYYMMDD_HHMMSS.csv
        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"InputLog_{timeStamp}_{logRecords[1].controlScheme}.csv";
        string filePath = Path.Combine(directoryPath, fileName);

        // Build CSV
        StringBuilder sb = new StringBuilder();
        
        // Generate CSV Header (For the dataset, its controller data will be removed)
        sb.AppendLine("ControlScheme,Time,MoveDeltaX,MoveDeltaY,AimDeltaX,AimDeltaY");

        foreach (var record in logRecords)
        {
            sb.AppendLine($"{record.controlScheme},{record.timeSinceLevelLoad},{record.moveDelta.x},{record.moveDelta.y},{record.aimDelta.x},{record.aimDelta.y}");
        }

        // Write file
        try
        {
            File.WriteAllText(filePath, sb.ToString());
            Debug.Log($"[InputDataLogger] 데이터가 성공적으로 저장되었습니다: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[InputDataLogger] 데이터 저장 중 오류 발생: {e.Message}");
        }

        // Clear list after saving
        logRecords.Clear();
    }

    private void OnDisable()
    {
        // If object or component is disabled, turn off and save file
        if (logRecords.Count > 0)
        {
            SaveDataToFile();
        }
    }
}