// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Stats.CreateAzureCdnWarehouseReports
{
    internal class RecentPopularityDetailByPackageReportBuilder
        : ReportBuilder
    {
        public RecentPopularityDetailByPackageReportBuilder(ILogger<RecentPopularityDetailByPackageReportBuilder> logger, string reportName, string reportArtifactName)
            : base(logger, reportName, reportArtifactName)
        {
        }

        protected override string JsonSerialize(DataTable table)
        {
            var jObject = MakeReportJson(table);
            AddTotalDownloads(jObject);
            SortItems(jObject);
            return jObject.ToString();
        }

        public static JObject MakeReportJson(DataTable data)
        {
            var report = new JObject();
            report.Add("Downloads", 0);

            var items = new JObject();
            foreach (DataRow row in data.Rows)
            {
                var packageVersion = (string)row[0];

                JObject childReport;
                JToken token;
                if (items.TryGetValue(packageVersion, out token))
                {
                    childReport = (JObject)token;
                }
                else
                {
                    childReport = new JObject();
                    childReport.Add("Downloads", 0);
                    childReport.Add("Items", new JArray());
                    childReport.Add("Version", packageVersion);

                    items.Add(packageVersion, childReport);
                }

                JObject obj = new JObject();

                if (row[1].ToString() == "NuGet" || row[1].ToString() == "WebMatrix")
                {
                    obj.Add("Client", string.Format("{0} {1}.{2}", row[2], row[3], row[4]));
                    obj.Add("ClientName", row[2].ToString());
                    obj.Add("ClientVersion", string.Format("{0}.{1}", row[3], row[4]));
                }
                else
                {
                    obj.Add("Client", row[2].ToString());
                    obj.Add("ClientName", row[2].ToString());
                    obj.Add("ClientVersion", "(unknown)");
                }

                if (row[5].ToString() != "(unknown)")
                {
                    obj.Add("Operation", row[5].ToString());
                }

                obj.Add("Downloads", (int)row[6]);

                ((JArray)childReport["Items"]).Add(obj);
            }

            report.Add("Items", items);

            return report;
        }

        private static int AddTotalDownloads(JObject report)
        {
            JToken token;
            if (!report.TryGetValue("Items", out token))
            {
                return (int) report["Downloads"];
            }

            var array = token as JArray;
            if (array != null)
            {
                var total = 0;
                foreach (JToken t in array)
                {
                    total += AddTotalDownloads((JObject)t);
                }
                report["Downloads"] = total;
                return total;
            }
            else
            {
                var total = 0;
                foreach (var child in ((JObject)token))
                {
                    total += AddTotalDownloads((JObject)child.Value);
                }
                report["Downloads"] = total;
                return total;
            }
        }

        private static void SortItems(JObject report)
        {
            var scratch = new List<Tuple<int, JObject>>();
            foreach (var child in ((JObject)report["Items"]))
            {
                scratch.Add(new Tuple<int, JObject>((int)child.Value["Downloads"], new JObject((JObject)child.Value)));
            }

            scratch.Sort((x, y) => x.Item1 == y.Item1 ? 0 : x.Item1 < y.Item1 ? 1 : -1);

            var items = new JArray();
            foreach (var item in scratch)
            {
                items.Add(item.Item2);
            }

            report["Items"] = items;
        }
    }
}