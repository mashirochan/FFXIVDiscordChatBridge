using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DynamisBridge
{
    public class LavalinkManager
    {
        private static Process? LavalinkProcess;

        public static void StartLavalink()
        {
            var currentDirectory = Environment.CurrentDirectory;
            var fullPath = Path.Combine(currentDirectory, "Lavalink.jar");
            Plugin.Logger.Debug($"Path to Lavalink.jar: {fullPath}");


            if (LavalinkProcess != null && !LavalinkProcess.HasExited)
            {
                Plugin.Logger.Debug("Lavalink is already running!");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-jar Lavalink.jar",
                WorkingDirectory = currentDirectory, // Set the directory where Lavalink.jar is located
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                LavalinkProcess = new Process { StartInfo = startInfo };
                LavalinkProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
                LavalinkProcess.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

                LavalinkProcess.Start();
                LavalinkProcess.BeginOutputReadLine();
                LavalinkProcess.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                Plugin.Logger.Error($"Error starting Lavalink.jar: {ex.Message}");
            }
        }

        public static void StopLavalink()
        {
            if (LavalinkProcess != null && !LavalinkProcess.HasExited)
            {
                LavalinkProcess.Kill();
                LavalinkProcess.Dispose();
                LavalinkProcess = null;
            }
        }

        public static bool IsLavalinkRunning()
        {
            return LavalinkProcess != null && !LavalinkProcess.HasExited;
        }
    }
}
