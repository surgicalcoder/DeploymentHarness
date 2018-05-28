using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace GoLive.DeploymentHarness
{
    public class ProcessManager
    {
        private static ILogger logger;

        public event EventHandler<OutputEventArg> OnOutput;

        public event EventHandler<OutputEventArg> OnError;

        public event EventHandler<ProcessIdEventArg> OnProcessCreated;

        public event EventHandler<TerminatedProcessEventArg> OnProcessTerminated;

        public string Run(string command, string args, bool redirectOutput = true)
        {
            return this.runInternal(command, args, (string) null, redirectOutput);
        }

        public string Run(string command, string args, string workingDirectory, bool redirectOutput = true)
        {
            return this.runInternal(command, args, workingDirectory, redirectOutput);
        }

        private string runInternal(string command, string args, string workingDirectory, bool redirectOutput)
        {
            var output = new StringBuilder();

            logger.LogInformation("Running {0} {1}", command, args);

            var process = new Process()
            {
                StartInfo =
                {
                    FileName = command
                }
            };

            if (args != null)
                process.StartInfo.Arguments = args;

            if (workingDirectory != null)
                process.StartInfo.WorkingDirectory = workingDirectory;

            process.StartInfo.RedirectStandardOutput = redirectOutput;
            process.StartInfo.RedirectStandardError = redirectOutput;
            process.StartInfo.UseShellExecute = false;

            process.OutputDataReceived += (sender, eventArgs) =>
            {
               if (eventArgs.Data == null)
                   return;

               output.AppendLine(eventArgs.Data);

                logger.LogInformation(" [O] {0}", eventArgs.Data);

               OnOutput?.Invoke(this, new OutputEventArg(eventArgs.Data));
            };

            process.ErrorDataReceived += (sender, eventArgs) =>
            {
               if (eventArgs.Data == null)
                   return;

               logger.LogInformation(" [E] {0}", eventArgs.Data);
               
               OnError?.Invoke(this, new OutputEventArg(eventArgs.Data));
           };

            process.Start();
            
            EventHandler<ProcessIdEventArg> onProcessCreated = this.OnProcessCreated;

            onProcessCreated?.Invoke(this, new ProcessIdEventArg(process.Id));

            if (redirectOutput)
            {
                process.BeginOutputReadLine();
            }

            process.WaitForExit();
            
            OnProcessTerminated?.Invoke(this, new TerminatedProcessEventArg(process.StartTime, process.ExitTime, process.ExitCode));

            return output.ToString();
        }

        private IEnumerable<Process> GetProcesses(string friendlyName, Func<Process, bool> query)
        {
            return Process.GetProcessesByName(friendlyName).Where(query);
        }

        public class OutputEventArg : EventArgs
        {
            public OutputEventArg(string logLine)
            {
                LogLine = logLine;
            }

            public string LogLine { get; set; }
        }

        public class ProcessIdEventArg : EventArgs
        {
            public ProcessIdEventArg(int processId)
            {
                ProcessId = processId;
            }

            public int ProcessId { get; set; }
        }

        public class TerminatedProcessEventArg : EventArgs
        {
            public TerminatedProcessEventArg(DateTime startTime, DateTime exitTime, int exitCode)
            {
                StartTime = startTime;
                ExitTime = exitTime;
                ExitCode = exitCode;
            }

            public DateTime StartTime { get; set; }

            public DateTime ExitTime { get; set; }

            public int ExitCode { get; set; }
        }
    }
}