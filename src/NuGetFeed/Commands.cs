// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetFeed
{
    /// <summary>
    /// base helper methods
    /// </summary>
    public partial class Commands
    {
        private void RunStep(BuildStep step)
        {
            step.OnLogMessage += OnLogMessage;
            step.OnProgress += OnProgress;

            step.Run();

            step.OnLogMessage -= OnLogMessage;
            step.OnProgress -= OnProgress;
        }

        private void OnProgress(object sender, BuildStepProgressEventArgs e)
        {
            BuildStep step = sender as BuildStep;
            Console.WriteLine("[{0}] {1}/{2}", step.Name, e.Complete, e.Total);
        }

        private void OnLogMessage(object sender, BuildStepLogEventArgs e)
        {
            if (e.Color.HasValue)
            {
                Console.ForegroundColor = e.Color.Value;
            }

            BuildStep step = sender as BuildStep;
            Console.WriteLine("[{0}] {1}", step.Name, e.Message);

            if (e.Color.HasValue)
            {
                Console.ResetColor();
            }

            if (e.FatalError)
            {
                Environment.Exit(1);
            }
        }
    }
}
