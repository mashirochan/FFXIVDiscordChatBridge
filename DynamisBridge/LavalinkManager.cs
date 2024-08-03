using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace DynamisBridge
{
    public class LavalinkManager
    {
        private Process? lavalinkProcess;

        public void StartLavalink()
        {
            if (lavalinkProcess != null && !lavalinkProcess.HasExited)
            {
                Plugin.Logger.Debug("Lavalink is already running!");
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = "-jar Lavalink.jar",
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), // Set the directory where Lavalink.jar is located
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            lavalinkProcess = new Process { StartInfo = startInfo };
            lavalinkProcess.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            lavalinkProcess.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

            lavalinkProcess.Start();
            lavalinkProcess.BeginOutputReadLine();
            lavalinkProcess.BeginErrorReadLine();
        }

        public void StopLavalink()
        {
            if (lavalinkProcess != null && !lavalinkProcess.HasExited)
            {
                lavalinkProcess.Kill();
                lavalinkProcess.Dispose();
                lavalinkProcess = null;
            }
        }

        public bool IsLavalinkRunning()
        {
            return lavalinkProcess != null && !lavalinkProcess.HasExited;
        }
    }
}
