using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Data.SqlClient
{
    public static class SqlConnectionStringBuilderExtensions
    {
        public static SqlConnectionStringBuilder ChangeDatabase(this SqlConnectionStringBuilder self, string newDatabaseName)
        {
            return new SqlConnectionStringBuilder(self.ConnectionString)
            {
                InitialCatalog = newDatabaseName
            };
        }

        public static Task<SqlConnection> ConnectTo(this SqlConnectionStringBuilder self)
        {
            return ConnectTo(self.ConnectionString);
        }

        public static Task<SqlConnection> ConnectToMaster(this SqlConnectionStringBuilder self)
        {
            return ConnectTo(self, "master");
        }

        public static Task<SqlConnection> ConnectTo(this SqlConnectionStringBuilder self, string databaseName)
        {
            var newConnectionString = new SqlConnectionStringBuilder(self.ConnectionString)
            {
                InitialCatalog = databaseName
            };
            return ConnectTo(newConnectionString.ConnectionString);
        }

        private static async Task<SqlConnection> ConnectTo(string connection)
        {
            var c = new SqlConnection(connection);
            await c.OpenAsync().ConfigureAwait(continueOnCapturedContext: false);
            return c;
        }

        public static void TrimNetworkProtocol(this SqlConnectionStringBuilder cstr)
        {
            int colonIndex = cstr.DataSource.IndexOf(':');
            if(colonIndex > -1 && colonIndex < cstr.DataSource.Length)
            {
                var trimmedcstr = cstr.DataSource.Substring(colonIndex + 1);
                cstr.DataSource = trimmedcstr;
            }
        }
    }
}
