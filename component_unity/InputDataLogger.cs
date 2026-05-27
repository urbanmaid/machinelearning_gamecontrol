using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputDataLogger : MonoBehaviour
{
    [Header("Input Action Names")]
    [Tooltip("Player Input 컴포넌트에 설정된 이동 액션의 이름")]
    public string moveActionName = "Move";
    [Tooltip("Player Input 컴포넌트에 설정된 조준 액션의 이름")]
    public string aimActionName = "Look";

    [Header("Log Settings")]
    public float saveInterval = 15f; // 15초마다 저장

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction aimAction;

    // 이전 프레임의 입력값 (변화량 계산용)
    private Vector2 lastMoveInput;
    private Vector2 lastAimInput;

    // 데이터 저장을 위한 구조체
    public struct InputLogRecord
    {
        public float timeSinceLevelLoad;
        public Vector2 moveDelta;
        public Vector2 aimDelta;
        public string controlScheme; // Gamepad 인지 KeyboardMouse 인지 구분하기 위함
    }

    private List<InputLogRecord> logRecords = new List<InputLogRecord>();
    private float timer = 0f;

    void Start()
    {
        // PlayerInput 컴포넌트 참조
        playerInput = GetComponent<PlayerInput>();

        // Action 참조
        moveAction = playerInput.actions[moveActionName];
        aimAction = playerInput.actions[aimActionName];

        if (moveAction == null || aimAction == null)
        {
            Debug.LogError("InputDataLogger: 설정한 액션 이름과 일치하는 Input Action을 찾을 수 없습니다.");
        }
    }

    void FixedUpdate()
    {
        if (moveAction == null || aimAction == null) return;

        Vector2 currentMoveInput = moveAction.ReadValue<Vector2>();
        Vector2 currentAimInput = aimAction.ReadValue<Vector2>();

        Vector2 moveDelta = currentMoveInput - lastMoveInput;
        Vector2 aimDelta = currentAimInput - lastAimInput;

        // 변화량이 있을 때만(또는 모든 프레임을) 기록. 
        // 여기서는 조작 차이 분석을 위해 매 프레임 수집합니다.
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

        // 다음 프레임을 위해 현재 값 저장
        lastMoveInput = currentMoveInput;
        lastAimInput = currentAimInput;

        // 3. 타이머 체크 및 파일 저장
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
        string directoryPath = Path.Combine(documentsPath, "charm_controllog");
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