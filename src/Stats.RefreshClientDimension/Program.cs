// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Jobs;
using Stats.ImportAzureCdnStatistics;

namespace Stats.RefreshClientDimension
{
    class Program
    {
        private static SqlConnectionStringBuilder _targetDatabase;

        static void Main(string[] args)
        {
            Trace.TraceInformation("Started...");
            var argsDictionary = ParseArgsDictionary(args);
            if (Init(argsDictionary))
            {
                Run().Wait();
            }
            Trace.TraceInformation("Finished");
            Console.ReadKey(true);
        }

        private static async Task Run()
        {
            try
            {
                using (var connection = await _targetDatabase.ConnectTo())
                {
                    // 0. get a distinct list of all useragents linked to (unknown) client dimension
                    var unknownUserAgents = (await Warehouse.GetUnknownUserAgents(connection)).ToList();

                    // 1. parse them and detect the ones that are recognized by the parser
                    var recognizedUserAgents = TryParseUnknownUserAgents(unknownUserAgents);

                    // 2. enumerate recognized user agents and ensure dimensions exists
                    var recognizedUserAgentsWithClientDimensionId = await Warehouse.EnsureClientDimensionsExist(connection, recognizedUserAgents);
                    var recognizedUserAgentsWithUserAgentId = await Warehouse.EnsureUserAgentFactsExist(connection, unknownUserAgents);

                    // 3. link the new client dimension to the facts
                    await Warehouse.PatchClientDimension(connection, recognizedUserAgents, recognizedUserAgentsWithClientDimensionId, recognizedUserAgentsWithUserAgentId);
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }

        private static IDictionary<string, ClientDimension> TryParseUnknownUserAgents(IEnumerable<string> unknownUserAgents)
        {
            var results = new Dictionary<string, ClientDimension>();
            foreach (var unknownUserAgent in unknownUserAgents)
            {
                var clientDimension = ClientDimension.FromUserAgent(unknownUserAgent);

                if (clientDimension != ClientDimension.Unknown)
                {
                    results.Add(unknownUserAgent, clientDimension);
                }
            }
            return results;
        }

        private static bool Init(IDictionary<string, string> argsDictionary)
        {
            try
            {
                var databaseConnectionString = JobConfigurationManager.GetArgument(argsDictionary, JobArgumentNames.StatisticsDatabase);
                _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

                return true;
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
            return false;
        }

        private static IDictionary<string, string> ParseArgsDictionary(string[] commandLineArgs)
        {
            if (commandLineArgs.Length > 0 && string.Equals(commandLineArgs[0], "-dbg", StringComparison.OrdinalIgnoreCase))
            {
                commandLineArgs = commandLineArgs.Skip(1).ToArray();
                Debugger.Launch();
            }

            // Get the args passed in or provided as an env variable based on jobName as a dictionary of <string argName, string argValue>
            var jobTraceListener = new JobTraceListener();
            Trace.Listeners.Add(jobTraceListener);

            var jobArgsDictionary = JobConfigurationManager.GetJobArgsDictionary(jobTraceListener, commandLineArgs, "Stats.RefreshClientDimension");
            return jobArgsDictionary;
        }
    }
}
