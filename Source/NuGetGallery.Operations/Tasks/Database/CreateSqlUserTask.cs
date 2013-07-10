using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace NuGetGallery.Operations.Tasks
{
    public class CreateSqlUserTask : DatabaseTask
    {
        [Option("The user name to create, leave the blank for the default", AltName="u")]
        public string UserName { get; set; }

        [Option("Set this switch to put the new Connection String in the clipboard", AltName="c")]
        public bool Clip { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (String.IsNullOrEmpty(UserName) && CurrentEnvironment != null)
            {
                UserName = String.Format("{0}-site-{1}", CurrentEnvironment.Name, DateTime.UtcNow.ToString("MMMdd-yyyy"));
            }
        }

        public override void ExecuteCommand()
        {
            // Generate password
            string password = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            WithMasterConnection((c, db) =>
            {
                if (!WhatIf)
                {
                    db.Execute(String.Format("CREATE LOGIN [{0}] WITH PASSWORD='{1}';", UserName, password));
                }
                Log.Info("Created Login: {0}", UserName);
            });

            WithConnection((c, db) =>
            {
                if (!WhatIf)
                {
                    db.Execute(String.Format("CREATE USER [{0}] FROM LOGIN [{0}];", UserName));
                }
                Log.Info("Created User: {0}", UserName);

                if (!WhatIf)
                {
                    db.Execute(String.Format("EXEC sp_addrolemember 'db_owner', '{0}';", UserName));
                }
                Log.Info("Added User to db_owner role: {0}", UserName);
            });

            // Generate the new connection string
            var newstr = new SqlConnectionStringBuilder(ConnectionString.ConnectionString);
            newstr.UserID = String.Format("{0}@{1}", UserName, Util.GetDatabaseServerName(ConnectionString));
            newstr.Password = password;

            if (Clip)
            {
                Clipboard.SetText(newstr.ConnectionString);
                Log.Info("Connection String for the new user is in the clipboard");
            }
            else
            {
                Log.Info("Connection String for the new user: ");
                Log.Info(newstr.ConnectionString);
            }
        }
    }
}
