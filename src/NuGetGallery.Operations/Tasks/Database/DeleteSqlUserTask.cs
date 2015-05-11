// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Common;

namespace NuGetGallery.Operations.Tasks.Database
{
    [Command("deletesqluser", "Lists SQL Users and access", AltName = "dsu")]
    public class DeleteSqlUser : DatabaseTask
    {
        [Option("Semicolon-separated list of users to delete", AltName="u")]
        public List<string> Users { get; set; }

        public DeleteSqlUser()
        {
            Users = new List<string>();
        }

        public override void ValidateArguments()
        {
            base.ValidateArguments();

            if (!Users.Any())
            {
                Users = null; // Just trigger ArgCheck to fail
            }

            ArgCheck.Required(Users, "Users");
        }

        public override void ExecuteCommand()
        {
            WithConnection((c, db) =>
            {
                foreach (var user in Users)
                {
                    if (db.Query<string>("SELECT name FROM sys.database_principals WHERE name = @n", new { n = user }).Any())
                    {
                        if (!WhatIf)
                        {
                            db.Execute(String.Format("DROP USER [{0}]", user));
                        }
                        Log.Info("Deleted Database User: {0}", user);
                    }
                    else
                    {
                        Log.Info("No DB User found: {0}", user);
                    }
                }
            });

            WithMasterConnection((c, db) =>
            {
                foreach (var user in Users)
                {
                    if (db.Query<string>("SELECT name FROM sys.sql_logins WHERE name = @n", new { n = user }).Any())
                    {
                        if (!WhatIf)
                        {
                            db.Execute(String.Format("DROP LOGIN [{0}]", user));
                        }
                        Log.Info("Deleted SQL Login: {0}", user);
                    }
                    else
                    {
                        Log.Info("No SQL Login found: {0}", user);
                    }
                }
            });
        }
    }
}
