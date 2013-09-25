using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Monitoring
{
    public abstract class SqlMonitorBase : ApplicationMonitor
    {
        public SqlConnectionStringBuilder ConnectionString { get; private set; }

        protected TimeSpan TimeToConnect { get; private set; }

        protected override string DefaultResourceName { get { return FormatResourceName(); } }

        protected SqlMonitorBase(string connectionString) : this(new SqlConnectionStringBuilder(connectionString)) { }
        protected SqlMonitorBase(SqlConnectionStringBuilder connectionString)
        {
            ConnectionString = connectionString;
        }
        

        protected virtual Task Connect(Action<SqlConnection> onConnect)
        {
            return Connect(c =>
            {
                onConnect(c);
                return TaskEx.FromResult(new object());
            });
        }

        protected virtual async Task Connect(Func<SqlConnection, Task> onConnect)
        {
            string connectionString = GetConnectionString();

            SqlConnection connection = null;
            try
            {
                try
                {
                    SqlConnection.ClearAllPools();
                    connection = new SqlConnection(connectionString);

                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    connection.Open();
                    sw.Stop();
                    TimeToConnect = sw.Elapsed;

                    await onConnect(connection);
                }
                finally
                {
                    if (connection != null)
                    {
                        connection.Dispose();
                        connection = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Failure(ex.Message);
            }
        }

        protected virtual string GetConnectionString()
        {
            return new SqlConnectionStringBuilder(ConnectionString.ConnectionString)
            {
                InitialCatalog = null
            }.ConnectionString;
        }

        protected virtual string FormatResourceName()
        {
            return "Server=" + ConnectionString.DataSource;
        }
    }
}
