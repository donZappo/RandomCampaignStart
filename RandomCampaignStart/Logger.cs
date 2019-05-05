using System;
using System.IO;
using System.Reflection;

namespace RandomCampaignStart
{
    public static class Logger
    {
        private static string LogFilePath => 
            Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName + 
            "\\RandomCampaignStart.log.txt";

        public static void Error(Exception ex)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine($"Message: {ex.Message}");
                writer.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        public static void LogDebug(string line)
        {
            if (!RandomCampaignStart.ModSettings.Debug) return;
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(line);
            }
        }

        public static void Log(string line)
        {
            using (var writer = new StreamWriter(LogFilePath, true))
            {
                writer.WriteLine(line);
            }
        }

        public static void Clear()
        {
            if (!RandomCampaignStart.ModSettings.Debug) return;
            using (var writer = new StreamWriter(LogFilePath, false))
            {
                writer.WriteLine($"{DateTime.Now.ToLongTimeString()} RandomCampaignStart Init");
            }
        }
    }
}