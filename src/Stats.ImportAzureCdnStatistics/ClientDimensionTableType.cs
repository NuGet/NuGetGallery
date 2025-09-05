// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Data;

namespace Stats.ImportAzureCdnStatistics
{
    internal static class ClientDimensionTableType
    {
        public static DataTable CreateDataTable(IDictionary<string, ClientDimension> clientDimensions)
        {
            var table = new DataTable();
            table.Columns.Add("UserAgent", typeof(string));
            table.Columns.Add("ClientName", typeof(string));
            table.Columns.Add("Major", typeof(string));
            table.Columns.Add("Minor", typeof(string));
            table.Columns.Add("Patch", typeof(string));

            foreach (var clientDimension in clientDimensions)
            {
                var row = table.NewRow();
                row["UserAgent"] = clientDimension.Key;
                row["ClientName"] = clientDimension.Value.ClientName;
                row["Major"] = clientDimension.Value.Major;
                row["Minor"] = clientDimension.Value.Minor;
                row["Patch"] = clientDimension.Value.Patch;

                table.Rows.Add(row);
            }
            return table;
        }
    }
}