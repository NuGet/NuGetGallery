// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Threading;

namespace NuGetGallery.Operations
{
    [Command("executeaggregatestatistics", "Executes the AggregateStatistics in the Gallery", AltName = "exaggstats", MaxArgs = 0, IsSpecialPurpose = true)]
    public class ExecuteAggregateStatisticsTask : DatabaseTask
    {
        public ExecuteAggregateStatisticsTask()
        {
        }

        public override void ExecuteCommand()
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("AggregateStatistics", connection);
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60 * 5;

                command.ExecuteScalar();
            }
        }
    }
}
