using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public enum FallScenarioType
{
    None,
    TripNearObstacle,
    FallFromStanding,
    FallDuringWalking,
    FallWhileStandingUp
}

public class FallScenarioManager : MonoBehaviour
{
    [Header("Subject")]
    public ElderlyActivityAgent elderlyAgent;
    public Transform subjectRoot;

    [Header("Scenario")]
    public bool runScenarioOnStart;
    public FallScenarioType scenarioType = FallScenarioType.None;
    public float fallDelaySeconds = 12f;
    public int scenarioSeed = 1;

    [Header("Data Logging")]
    public bool writeCsvLog = true;
    public float sampleInterval = 0.1f;
    public string outputFolderName = "FallDetectionSyntheticLogs";

    readonly List<string> rows = new List<string>();
    Coroutine loggingRoutine;

    void Reset()
    {
        elderlyAgent = FindAnyObjectByType<ElderlyActivityAgent>();
        if (elderlyAgent != null)
        {
            subjectRoot = elderlyAgent.transform;
        }
    }

    void Start()
    {
        UnityEngine.Random.InitState(scenarioSeed);

        if (elderlyAgent == null)
        {
            elderlyAgent = FindAnyObjectByType<ElderlyActivityAgent>();
        }

        if (subjectRoot == null && elderlyAgent != null)
        {
            subjectRoot = elderlyAgent.transform;
        }

        if (writeCsvLog)
        {
            loggingRoutine = StartCoroutine(LogSubjectPose());
        }

        if (runScenarioOnStart && scenarioType != FallScenarioType.None)
        {
            StartCoroutine(RunFallScenario());
        }
    }

    public void RunScenarioNow(FallScenarioType type)
    {
        scenarioType = type;
        StartCoroutine(RunFallScenario());
    }

    IEnumerator RunFallScenario()
    {
        yield return new WaitForSeconds(fallDelaySeconds);

        if (elderlyAgent != null)
        {
            elderlyAgent.TriggerFall();
        }
    }

    IEnumerator LogSubjectPose()
    {
        rows.Clear();
        rows.Add("time,scenario,activity,position_x,position_y,position_z,rotation_y");

        while (enabled)
        {
            if (subjectRoot != null)
            {
                Vector3 position = subjectRoot.position;
                float yaw = subjectRoot.eulerAngles.y;
                string activity = elderlyAgent != null ? elderlyAgent.CurrentActivity.ToString() : "Unknown";

                rows.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F3},{1},{2},{3:F3},{4:F3},{5:F3},{6:F3}",
                    Time.time,
                    scenarioType,
                    activity,
                    position.x,
                    position.y,
                    position.z,
                    yaw));
            }

            yield return new WaitForSeconds(sampleInterval);
        }
    }

    void OnApplicationQuit()
    {
        SaveLog();
    }

    public void SaveLog()
    {
        if (!writeCsvLog || rows.Count <= 1)
        {
            return;
        }

        string folder = Path.Combine(Application.persistentDataPath, outputFolderName);
        Directory.CreateDirectory(folder);

        string fileName = string.Format(
            CultureInfo.InvariantCulture,
            "scenario_{0}_{1:yyyyMMdd_HHmmss}.csv",
            scenarioType,
            DateTime.Now);

        File.WriteAllLines(Path.Combine(folder, fileName), rows);
    }

    void OnDisable()
    {
        if (loggingRoutine != null)
        {
            StopCoroutine(loggingRoutine);
        }
    }
}
