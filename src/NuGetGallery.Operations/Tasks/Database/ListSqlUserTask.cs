// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Database
{
    [Command("listsqluser", "Lists SQL Users and access", AltName = "lsu")]
    public class ListSqlUserTask : DatabaseTask
    {
        public override void ExecuteCommand()
        {
            ISet<string> dbUsers = null;
            ISet<string> sqlLogins = null;
            WithMasterConnection((c, db) =>
            {
                sqlLogins = new HashSet<string>(db.Query<string>("SELECT name FROM sys.sql_logins"));
            });

            WithConnection((c, db) =>
            {
                dbUsers = new HashSet<string>(db.Query<string>("SELECT name FROM sys.database_principals WHERE type = 'S'"));
            });

            Debug.Assert(dbUsers != null && sqlLogins != null);

            var sa = sqlLogins.SingleOrDefault(s => s.EndsWith("sa", StringComparison.Ordinal));
            Log.Info("SA Login Name: {0}", sa);

            var pairs = dbUsers.Where(s => sqlLogins.Contains(s));
            Log.Info("SQL Logins with an associated DB User:");
            foreach (var pair in pairs)
            {
                Log.Info("* {0}", pair);
            }
            
            var orphanedLogins = sqlLogins.Except(dbUsers).Except(new [] { sa });
            if (orphanedLogins.Any())
            {
                Log.Info("'Orphaned' Logins that should be deleted:");
                foreach (var login in orphanedLogins)
                {
                    Log.Info("* {0}", login);
                }
            }

            var orphanedUsers = dbUsers.Except(sqlLogins).Except(new[] { "dbo", "guest", "INFORMATION_SCHEMA", "sys" });
            if (orphanedUsers.Any())
            {
                Log.Info("DB Users without an attached SQL Login that should be deleted:");
                foreach (var user in orphanedUsers)
                {
                    Log.Info("* {0}", user);
                }
            }
        }
    }
}
