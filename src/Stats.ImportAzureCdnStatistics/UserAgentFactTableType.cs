// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;

namespace Stats.ImportAzureCdnStatistics
{
    internal static class UserAgentFactTableType
    {
        public static DataTable CreateDataTable(IDictionary<string, UserAgentFact> userAgentFacts)
        {
            var table = new DataTable();
            table.Columns.Add("UserAgent", typeof(string));

            foreach (var userAgentFact in userAgentFacts)
            {
                var row = table.NewRow();
                row["UserAgent"] = userAgentFact.Key;

                table.Rows.Add(row);
            }
            return table;
        }
    }
}