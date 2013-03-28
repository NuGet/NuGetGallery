using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Threading;

namespace NuGetGallery.Operations
{
    [Command("executeaggregatestatistics", "Executes the AggregateStatistics in the Gallery", AltName = "exaggstats", MaxArgs = 0)]
    public class ExecuteAggregateStatisticsTask : DatabaseTask
    {
        public ExecuteAggregateStatisticsTask()
        {
        }

        public override void ExecuteCommand()
        {
            using (SqlConnection connection = new SqlConnection(ConnectionString))
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
