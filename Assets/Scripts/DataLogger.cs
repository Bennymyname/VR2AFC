using System;
using System.IO;
using System.Text;
using UnityEngine;

public class DataLogger
{
    private readonly string directory;
    private readonly string filePath;
    private readonly StringBuilder sb = new StringBuilder();
    private bool wroteHeader;

    public string FilePath => filePath;

    public DataLogger(string resultsFolderName = "2AFC results", bool preferAssetsFolder = true, string prefix = "2AFC_P")
    {
        string dir;
#if UNITY_EDITOR
        if (preferAssetsFolder)
            dir = Path.Combine(Application.dataPath, resultsFolderName);
        else
            dir = Application.persistentDataPath;
#else
        dir = Application.persistentDataPath;
#endif
        directory = dir;
        Directory.CreateDirectory(directory);

        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        filePath = Path.Combine(directory, $"{prefix}_{ts}.csv");
    }

    public void LogHeaderIfNeeded()
    {
        if (wroteHeader) return;
        sb.AppendLine("trial,stdSide,cmpPx,response,correct,rtMs,lowPx,midPx,highPx,comment");
        wroteHeader = true;
    }

    public void LogTrial(
        int trial, string stdSide, int cmpPx,
        string response, bool correct, long rtMs,
        int lowPx, int midPx, int highPx, string comment = "")
    {
        LogHeaderIfNeeded();
        sb.AppendLine($"{trial},{stdSide},{cmpPx},{response},{correct},{rtMs},{lowPx},{midPx},{highPx},{comment}");
    }

    public void LogSummary(string key, string value)
    {
        LogHeaderIfNeeded();
        sb.AppendLine($",,,,,,,,{key},{value}");
    }

    public void SaveNow()
    {
        try
        {
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[TwoAFC] log saved: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TwoAFC] failed to write CSV: {filePath}\n{e}");
        }
    }
}
