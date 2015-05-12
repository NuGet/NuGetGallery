using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.IO;
using NuGet.Jobs.Common;

namespace Stats.RebuildWarehouseIndexes
{
    internal class Job : JobBase
    {

        public Job() : base() { }

        public SqlConnectionStringBuilder WarehouseConnection { get; set; }
        public int CommandTimeOut { get; set; }
       
        public override async Task<bool> Run()
        {
            using (var connection = await WarehouseConnection.ConnectTo())
            {
                Trace.TraceInformation(String.Format("Rebuilding Indexes in {0}/{1}", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog));
                
                    SqlCommand rebuild = connection.CreateCommand();
                    rebuild.CommandText = "RebuildIndexes";
                    rebuild.CommandTimeout = CommandTimeOut > 0 ? CommandTimeOut :
                        60 * // seconds
                        60 * // minutes
                        8;   // hours

                    await rebuild.ExecuteNonQueryAsync();
                    Trace.TraceInformation(String.Format("Rebuilt Indexes in {0}/{1}", WarehouseConnection.DataSource, WarehouseConnection.InitialCatalog));
            }

            return true;
        }
      
        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {

            WarehouseConnection =
                    new SqlConnectionStringBuilder(
                        JobConfigManager.GetArgument(jobArgsDictionary,
                            JobArgumentNames.DestinationDatabase,
                            EnvironmentVariableKeys.SqlWarehouse));

            string commandTimeOutString = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.CommandTimeOut);

            if (String.IsNullOrEmpty(commandTimeOutString))
            {
                CommandTimeOut = 0;
            }
            else
            {
                int cmdTimeout = Convert.ToInt32(commandTimeOutString);
                CommandTimeOut = cmdTimeout > 0 ? cmdTimeout : 0; 
            }

            return true;

        }

    }
}
