using System;
using System.IO;
using System.Text;

namespace LaptopKeyboardLocker.Core
{
    /// <summary>
    /// 企業級輕量結構化 JSON 診斷日誌模組。
    /// 遵循零按鍵記錄 (Zero Keystroke Logging) 與資料自動清理規範（超過 512 KB 自動輪替清除）。
    /// </summary>
    public static class DiagnosticLogger
    {
        private const long MaxLogFileBytes = 512 * 1024; // 512 KB 限制
        private static readonly string SessionTraceId = Guid.NewGuid().ToString("N").Substring(0, 12);
        private static readonly object SyncLock = new object();
        private static string resolvedLogPath;

        public static string TraceId
        {
            get { return SessionTraceId; }
        }

        private static string GetLogFilePath()
        {
            if (!string.IsNullOrEmpty(resolvedLogPath))
            {
                return resolvedLogPath;
            }

            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.jsonl");
            try
            {
                // 測試同目錄寫入權限
                File.AppendAllText(portablePath, string.Empty, Encoding.UTF8);
                resolvedLogPath = portablePath;
                return resolvedLogPath;
            }
            catch
            {
                string appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LaptopKeyboardLocker"
                );
                try
                {
                    if (!Directory.Exists(appDataDir))
                    {
                        Directory.CreateDirectory(appDataDir);
                    }
                    resolvedLogPath = Path.Combine(appDataDir, "diagnostics.jsonl");
                }
                catch
                {
                    resolvedLogPath = portablePath;
                }
                return resolvedLogPath;
            }
        }

        public static void Info(string eventName, string detail)
        {
            WriteLog("INFO", eventName, detail);
        }

        public static void Warn(string eventName, string detail)
        {
            WriteLog("WARN", eventName, detail);
        }

        public static void Error(string eventName, string detail)
        {
            WriteLog("ERROR", eventName, detail);
        }

        private static void WriteLog(string level, string eventName, string detail)
        {
            try
            {
                lock (SyncLock)
                {
                    string logPath = GetLogFilePath();

                    // 資料生命週期控管：超過 512 KB 時自動清理重建
                    if (File.Exists(logPath))
                    {
                        FileInfo fi = new FileInfo(logPath);
                        if (fi.Length > MaxLogFileBytes)
                        {
                            File.Delete(logPath);
                        }
                    }

                    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
                    string safeEvent = EscapeJson(eventName ?? string.Empty);
                    string safeDetail = EscapeJson(detail ?? string.Empty);

                    string jsonLine = string.Format(
                        "{{\"timestamp\":\"{0}\",\"traceId\":\"{1}\",\"level\":\"{2}\",\"event\":\"{3}\",\"detail\":\"{4}\"}}{5}",
                        timestamp,
                        SessionTraceId,
                        level,
                        safeEvent,
                        safeDetail,
                        Environment.NewLine
                    );

                    File.AppendAllText(logPath, jsonLine, Encoding.UTF8);
                }
            }
            catch
            {
                // 日誌寫入失敗不得影響主程序穩定運作
            }
        }

        public static string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}

