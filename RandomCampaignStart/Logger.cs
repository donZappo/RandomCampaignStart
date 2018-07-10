using System;
using System.IO; 

namespace RandomCampaignStart
{
    // 'borrowed' from Morphyum
    public class Logger
    {
        static string filePath = $"{RngStart.Settings.ModDirectory}/Log.txt";
        public static void LogError(Exception ex)
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine("Message :" + ex.Message + "<br/>" + Environment.NewLine + "StackTrace :" + ex.StackTrace +
                   "" + Environment.NewLine + "Date :" + DateTime.Now.ToString());
                writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
            }
        }

        public static void LogLine(object line)
        {
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine($"{DateTime.Now.ToShortTimeString()} {line}");
            }
        }

        public static void Debug(object line)
        {
            if (!RngStart.Settings.Debug) return;
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine($"{DateTime.Now.ToShortTimeString()} {line}");
            }
        }
    }
}