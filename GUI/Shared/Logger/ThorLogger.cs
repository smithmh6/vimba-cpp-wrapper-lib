using System;
using System.IO;
using System.Text;
using FilterWheelShared.Common;
using ThorLogWrapper;

namespace FilterWheelShared.Logger
{
    public class ThorLogger
    {
        private const string configName = "LogConfig.conf";
        private ThorLogger()
        {
        }

        static ThorLogger()
        {
            _instance = new ThorLogger();
        }

        private static readonly ThorLogger _instance = null;

        public static ThorLogger Instance => _instance;

        public static void UpdateLogPath(string name)
        {
            var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configName);
            string temp = string.IsNullOrEmpty(name) ? "Common" : name;
            var path = $"{ThorlabsProduct.LocalApplicationDataDir}{temp}-";
            string text = File.ReadAllText(configFile).Replace("${LogPath}", path);
            byte[] configText = Encoding.UTF8.GetBytes(text);
            NativeLoggerWrapper.SetLogConfig(configText);
        }

        public static void Log(string customMessage, Exception e, ThorLogLevel level)
        {
            NativeLoggerWrapper.WriteLog(level, Encoding.UTF8.GetBytes($"{customMessage}. Exception message: {e.Message}"));
        }

        public static void Log(string message, ThorLogLevel level)
        {
            NativeLoggerWrapper.WriteLog(level, Encoding.UTF8.GetBytes(message));
        }
    }

}
