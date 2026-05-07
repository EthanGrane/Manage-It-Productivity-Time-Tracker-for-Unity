using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTimeTracker {
    [InitializeOnLoad]
    public static class EditorTimeTracker
    {
        const string DATE_FORMAT = "yyyy-MM-dd HH:mm:ss";
        const double CHECK_INTERVAL_MINUTES = 1;
        const double SESSION_GAP_MINUTES = 15;

        static double nextCheckTime;

        static string FilePath => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TimeTracking.json")
        );

        static EditorTimeTracker()
        {
            EditorApplication.update += OnFirstUpdate;
        }

        static void OnFirstUpdate()
        {
            EditorApplication.update -= OnFirstUpdate;
            EditorApplication.update += OnUpdate;

            try
            {
                TimeTrackingData data = LoadData();
                WorkSession current = GetCurrentSession(data);

                if (current == null)
                {
                    data.sessions.Add(new WorkSession
                    {
                        start = DateTime.Now.ToString(DATE_FORMAT),
                        lastCheck = DateTime.Now.ToString(DATE_FORMAT)
                    });
                    SaveData(data);
                    Debug.Log($"[TimeTracker] Nueva sesión: {DateTime.Now.ToString(DATE_FORMAT)}");
                }
                else
                {
                    // Debug.Log($"[TimeTracker] Sesión activa desde: {current.start}");
                }

                nextCheckTime = EditorApplication.timeSinceStartup + CHECK_INTERVAL_MINUTES * 60;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TimeTracker] Error en init: {e.Message}\n{e.StackTrace}");
            }
        }

        static void OnUpdate()
        {
            if (EditorApplication.timeSinceStartup < nextCheckTime) return;

            nextCheckTime = EditorApplication.timeSinceStartup + CHECK_INTERVAL_MINUTES * 60;

            try
            {
                TimeTrackingData data = LoadData();
                WorkSession current = GetCurrentSession(data);

                if (current == null)
                {
                    data.sessions.Add(new WorkSession
                    {
                        start = DateTime.Now.ToString(DATE_FORMAT),
                        lastCheck = DateTime.Now.ToString(DATE_FORMAT)
                    });
                    current = data.sessions.Last();
                    Debug.Log($"[TimeTracker] Nueva sesión (gap > {SESSION_GAP_MINUTES}min)");
                }

                current.lastCheck = DateTime.Now.ToString(DATE_FORMAT);
                SaveData(data);
                // Debug.Log($"[TimeTracker] Check: {current.lastCheck}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[TimeTracker] Error en check: {e.Message}");
            }
        }

        static WorkSession GetCurrentSession(TimeTrackingData data)
        {
            if (data.sessions.Count == 0) return null;

            WorkSession last = data.sessions.Last();

            string lastActivityStr = !string.IsNullOrEmpty(last.lastCheck)
                ? last.lastCheck
                : last.start;

            if (!DateTime.TryParse(lastActivityStr, out DateTime lastActivity))
                return null;

            return (DateTime.Now - lastActivity).TotalMinutes < SESSION_GAP_MINUTES
                ? last
                : null;
        }

        static TimeTrackingData LoadData()
        {
            if (!File.Exists(FilePath)) return new TimeTrackingData();
            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return new TimeTrackingData();
            return JsonUtility.FromJson<TimeTrackingData>(json) ?? new TimeTrackingData();
        }

        static void SaveData(TimeTrackingData data)
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, prettyPrint: true));
        }
    }
}
