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
        public string Server { get; private set; }
        public string User { get; private set; }
        public string Password { get; private set; }

        protected TimeSpan TimeToConnect { get; private set; }

        protected override string DefaultResourceName { get { return FormatResourceName(); } }
        
        protected SqlMonitorBase(string server, string user, string password) {
            Server = server;
            User = user;
            Password = password;
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
            SqlConnectionStringBuilder builder = BuildConnectionString();
            
            SqlConnection connection = null;
            try
            {
                try
                {
                    SqlConnection.ClearAllPools();
                    connection = new SqlConnection(builder.ConnectionString);

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

        protected virtual SqlConnectionStringBuilder BuildConnectionString()
        {
            string connStr = String.Format(
                        "Server=tcp:{0};" +
                        "User ID={1};" +
                        "Password={2};" +
                        "Trusted_Connection=False;" +
                        "Encrypt=True;",
                        Server,
                        User,
                        Password);
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connStr);
            return builder;
        }

        protected virtual string FormatResourceName()
        {
            return "Server=" + Server;
        }
    }
}
