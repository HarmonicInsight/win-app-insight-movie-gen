using System;

namespace InsightMovie.Services
{
    public class AppLogger : IAppLogger
    {
        public event Action<string>? LogReceived;

        public void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"[{timestamp}] {message}";
            LogReceived?.Invoke(line);
        }

        public void LogError(string message, Exception? ex = null)
        {
            var detail = ex != null ? $"{message}: {ex.Message}" : message;
            Log(detail);
        }
    }
}
