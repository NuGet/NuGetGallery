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
        private static string _targetClientName;
        private static string _userAgentFilter;

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
                    IDictionary<string, Tuple<int, int>> linkedUserAgents;

                    // This dictionary uses the user agent string as the key.
                    // The first item of the Tuple-value contains the user agent ID.
                    // The seconds item in the Tuple will contain the ClientDimension as parsed from the user agent using knownclients.yaml.
                    IDictionary<string, Tuple<int, ClientDimension>> currentUserAgentInfo = null;

                    if (string.IsNullOrWhiteSpace(_targetClientName))
                    {
                        // Patch only unknown clients

                        // 0.   Get a distinct collection of all useragents linked to (unknown) client
                        linkedUserAgents = await Warehouse.GetUnknownUserAgents(connection);

                        if (linkedUserAgents.Any())
                        {
                            // 1. Parse them and detect the ones that are recognized by the parser
                            currentUserAgentInfo = ParseUserAgentsAndLinkToClientDimension(linkedUserAgents);
                        }
                    }
                    else
                    {
                        // Patch only clients already linked to the TargetClientName

                        // 0. Get a distinct collection of all useragents linked to TargetClientName
                        linkedUserAgents = await Warehouse.GetLinkedUserAgents(connection, _targetClientName, _userAgentFilter);

                        if (linkedUserAgents.Any())
                        {
                            // 1.   Parse them and detect the ones that are recognized by the parser
                            //      These user agents are linked to newly parsed client dimensions.
                            currentUserAgentInfo = ParseUserAgentsAndLinkToClientDimension(linkedUserAgents);
                        }
                    }

                    if (currentUserAgentInfo != null && currentUserAgentInfo.Any())
                    {
                        // 2.   Enumerate recognized user agents and ensure dimensions exists
                        //      This resultset may contain updated links between user agent and client dimension id.
                        var updatedUserAgentInfo = await Warehouse.EnsureClientDimensionsExist(connection, currentUserAgentInfo);

                        // 3.   Determine the updated links between user agent and client dimension ID
                        //      by comparing the resultset of step 2 with the original links found in step 0.

                        var changedLinks = FindChangedLinksBetweenUserAgentAndClientDimensionId(currentUserAgentInfo, updatedUserAgentInfo);

                        // 4. Link the new client dimension to the facts
                        await Warehouse.PatchClientDimension(connection, changedLinks);
                    }
                }
            }
            catch (Exception exception)
            {
                Trace.TraceError(exception.ToString());
            }
        }

        private static IReadOnlyCollection<UserAgentToClientDimensionLink> FindChangedLinksBetweenUserAgentAndClientDimensionId(IDictionary<string, Tuple<int, ClientDimension>> currentUserAgentInfo, IDictionary<string, int> updatedUserAgentInfo)
        {
            var resultSet = new List<UserAgentToClientDimensionLink>();
            foreach (var currentInfo in currentUserAgentInfo)
            {
                var userAgent = currentInfo.Key;
                var currentClientDimensionId = currentInfo.Value.Item2.Id;

                // 0.   Find the matching element in the updated info.
                var updatedClientDimensionId = updatedUserAgentInfo[userAgent];
                if (currentClientDimensionId != updatedClientDimensionId)
                {
                    var userAgentId = currentInfo.Value.Item1;
                    var result = new UserAgentToClientDimensionLink(userAgent, userAgentId, currentClientDimensionId, updatedClientDimensionId);
                    resultSet.Add(result);
                }
            }
            return resultSet;
        }

        private static IDictionary<string, Tuple<int, ClientDimension>> ParseUserAgentsAndLinkToClientDimension(IDictionary<string, Tuple<int, int>> userAgentInfo)
        {
            var results = new Dictionary<string, Tuple<int, ClientDimension>>();
            foreach (var item in userAgentInfo)
            {
                var useragent = item.Key;
                var userAgentId = item.Value.Item1;
                var clientDimensionId = item.Value.Item2;

                var clientDimension = ClientDimension.FromUserAgent(useragent);

                clientDimension.Id = clientDimensionId;
                results.Add(item.Key, new Tuple<int, ClientDimension>(userAgentId, clientDimension));
            }
            return results;
        }

        private static bool Init(IDictionary<string, string> argsDictionary)
        {
            try
            {
                var databaseConnectionString = JobConfigurationManager.GetArgument(argsDictionary, JobArgumentNames.StatisticsDatabase);
                _targetDatabase = new SqlConnectionStringBuilder(databaseConnectionString);

                _targetClientName = JobConfigurationManager.TryGetArgument(argsDictionary, "TargetClientName");
                _userAgentFilter = JobConfigurationManager.TryGetArgument(argsDictionary, "UserAgentFilter");

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
