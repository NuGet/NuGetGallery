using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    public abstract class BuildStep : IDisposable
    {
        public event EventHandler<BuildStepLogEventArgs> OnLogMessage;
        public event EventHandler<BuildStepProgressEventArgs> OnProgress;

        public string Name { get; private set; }

        public Config Config { get; private set; }

        protected BuildStep(Config config, string stepName)
        {
            Name = stepName;
            Config = config;
        }

        public void Run()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            RunCore();

            timer.Stop();

            Log(String.Format(CultureInfo.InvariantCulture, "Completed in {0}", timer.Elapsed), ConsoleColor.Green);
        }

        protected void Log(string message, ConsoleColor? color=null)
        {
            if (OnLogMessage != null)
            {
                OnLogMessage(this, new BuildStepLogEventArgs(message, color));
            }
        }

        protected void LogFatalError(string message)
        {
            if (OnLogMessage != null)
            {
                OnLogMessage(this, new BuildStepLogEventArgs(message, true));
            }
        }

        protected void ProgressUpdate(int complete, int total)
        {
            if (OnProgress != null)
            {
                OnProgress(this, new BuildStepProgressEventArgs(complete, total));
            }
        }

        protected void CreateDir(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);

            if (!dir.Exists)
            {
                Log("Creating " + dir.FullName);
                dir.Create();
            }
        }

        protected abstract void RunCore();

        public virtual void Dispose()
        {
            // do nothing by default
        }
    }

    public class BuildStepLogEventArgs : EventArgs
    {
        public bool FatalError { get; private set; }

        public string Message { get; private set; }

        public ConsoleColor? Color { get; private set; }

        public BuildStepLogEventArgs(string message, ConsoleColor? color=null)
        {
            Message = message;
            Color = color;
        }

        public BuildStepLogEventArgs(string message, bool fatalError)
        {
            Message = message;
            Color = ConsoleColor.Red;
            FatalError = fatalError;
        }
    }

    public class BuildStepProgressEventArgs : EventArgs
    {
        public int Complete { get; private set; }

        public int Total { get; private set; }

        public BuildStepProgressEventArgs(int complete, int total)
        {
            Complete = complete;
            Total = total;
        }

        public int Remaining
        {
            get
            {
                return Total - Complete;
            }
        }

        public double Percent
        {
            get
            {
                return Complete / Total;
            }
        }
    }
}
