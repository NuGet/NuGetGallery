// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
//*******************************************************************************************************************************
// IMPORTANT: To run the tests please do the following steps
//0. Copy all the files from TestFiles folder to bin\debug folder
//1. Run a windows powershell console as adminstrator
//2. Do Import-Module Functions.ps1
//3. Run the following command
//-- Install-NuGetService -serviceName "myCustom" -serviceTitle "myCustom" -scriptToRun FULL_PATH_FOR_myCustom.cmd in bin\debug folder
//4. Run the test exe with -DashboardStorageAccount info (this is the nugetdashboard storage account string)
//*******************************************************************************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Heartbeat;
using NuGet.Jobs.Common;
using System.IO;
using Xunit;

namespace Tests.Heartbeat
{
    class Program
    {
        public const string customServiceName = "myCustom";

        static void Main(string[] args)
        {
            try
            {
                CreateConfigFileForTests();
                HeartbeatJob heartbeatjob = new HeartbeatJob();
                IDictionary<string, string> jobArgsDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                jobArgsDictionary.Add(JobArgumentNames.Once, "-once");
                jobArgsDictionary.Add(JobArgumentNames.LogFileSuffix, "CustomTest");

                if (args.Count() > 0)
                {
                    jobArgsDictionary.Add(JobArgumentNames.DashboardStorageAccount, args[0]);
                }

                //Create event log entry and Start notepad
                CreateEventLogEntry();
                uint processId = heartbeatjob.GetProcessIdFromServiceName(customServiceName);
                Assert.True(processId != 0, String.Format("Service {0} is launched successfully", customServiceName));

                heartbeatjob.Init(jobArgsDictionary);

                //First test: We should find the entry in event log and leave the process alone. 
                heartbeatjob.Run();
                Assert.True(VerifyTestCustomProcessIsRunning(processId), String.Format("Process with processId {0} and service name {1} is running as expected", processId, customServiceName));

                //Sleep for the threshhold specified in config file
                Thread.Sleep(1000 * 60);

                //The job tolerates for sometime if it is unable to retrieve tracing
                heartbeatjob.Init(jobArgsDictionary);
                heartbeatjob.Run();
                Assert.True(VerifyTestCustomProcessIsRunning(processId), String.Format("Process with processId {0} and service name {1} is running as expected", processId, customServiceName));

                //Sleep for longer then threshhold for retry logic to finish and the process to get killed
                Thread.Sleep(2000 * 60);
                heartbeatjob.Run();
                Thread.Sleep(5000);
                Assert.True(!VerifyTestCustomProcessIsRunning(processId), String.Format("Process with processId {0} and service name {1} is killed as expected", processId, customServiceName));

            }
            catch (System.NullReferenceException)
            {
                Console.WriteLine("USAGE: Please specify DashboardStorageAccount switch to run the tests successfully");
            }
        }

        private static bool VerifyTestCustomProcessIsRunning(uint processId)
        {
            return Process.GetProcesses().Where(p => p.Id == processId).Count() == 1;
        }

        private static void CreateConfigFileForTests()
        {
            //Create the config file to be used
            var configFile = new StreamWriter("heartbeat.config");
            configFile.WriteLine(String.Format("{0}, {1}", customServiceName, "1"));
            configFile.Close();

        }

        private static void CreateEventLogEntry()
        {
            try
            {
                // Create an instance of EventLog
                System.Diagnostics.EventLog eventLog = new System.Diagnostics.EventLog();
                string eventSource = customServiceName;

                if (!EventLog.SourceExists(eventSource))
                {
                    System.Diagnostics.EventLog.CreateEventSource(eventSource, "Application");
                }

                // Set the source name for writing log entries.
                eventLog.Source = eventSource;

                // Create an event ID to add to the event log
                int eventID = 8;

                // Write an entry to the event log.
                eventLog.WriteEntry("Test",
                                    System.Diagnostics.EventLogEntryType.Error,
                                    eventID);

                // Close the Event Log
                eventLog.Close();
            }
            catch (System.Security.SecurityException e)
            {
                Console.WriteLine("Run VS or exe as an administrator to be able to read/create an event log entry");
            }
        }
    }
}
