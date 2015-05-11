// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks
{
    [Command("createsqluser", "Creates a new DB Owner for the gallery database", AltName="csu")]
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
                UserName = String.Format("{0}-site-{1}", CurrentEnvironment.EnvironmentName, DateTime.UtcNow.ToString("MMMdd-yyyy"));
            }

            ArgCheck.RequiredOrConfig(UserName, "UserName");
        }

        public override void ExecuteCommand()
        {
            // Generate password
            var rng = new RNGCryptoServiceProvider();
            byte[] data = new byte[20];
            rng.GetBytes(data);
            string password = Convert.ToBase64String(data);

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
                var t = new Thread(() => Clipboard.SetText(newstr.ConnectionString));
                t.SetApartmentState(ApartmentState.STA);
                t.Start();
                t.Join();
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
