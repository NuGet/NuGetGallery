using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using System.Data.SqlClient;

namespace NuGetGallery
{
    public class CreateDatabase : Task
    {
        public override bool Execute()
        {
            var sqlConnectionBuilder = new SqlConnectionStringBuilder(ConnectionString);
            var initialCatalog = sqlConnectionBuilder.InitialCatalog;
            
            sqlConnectionBuilder.InitialCatalog = string.Empty;

            var connectionString = sqlConnectionBuilder.ToString();
            
            if (string.IsNullOrWhiteSpace(initialCatalog)) {
                Log.LogError("The connection string must specify an initial catalog.");
                return false;
            }

            // TODO: it would be nice to check if the database exists firsts and not show this message if so
            Log.LogMessage("Conditionally creating database '{0}' on connection '{1}'.", initialCatalog, connectionString);

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(string.Format("IF NOT EXISTS(SELECT * FROM sys.databases WHERE name = '{0}') BEGIN CREATE DATABASE [{0}] END", initialCatalog), connection)) {
                connection.Open();
                command.ExecuteNonQuery();
            }

            Log.LogMessage("Conditionally Created database '{0}' on connection '{1}'.", initialCatalog, connectionString);
            
            return true;
        }

        [Required]
        public string ConnectionString { get; set; }
    }
}
