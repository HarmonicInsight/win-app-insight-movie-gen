using System;

namespace InsightCast.Services
{
    public interface IAppLogger
    {
        event Action<string>? LogReceived;
        void Log(string message);
        void LogError(string message, Exception? ex = null);
    }
}
