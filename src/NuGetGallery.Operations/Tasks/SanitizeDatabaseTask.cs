// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Helpers;
using AnglicanGeek.DbExecutor;

namespace NuGetGallery.Operations.Tasks
{
    [Command("sanitizedatabase", "Cleans Personally-Identified Information out of a database without destroying data", AltName = "sdb", MinArgs = 0, MaxArgs = 0)]
    public class SanitizeDatabaseTask : DatabaseTask
    {
        private const string SanitizeUsersQuery = @"
                UPDATE Users
                SET    ApiKey = NEWID(),
                       EmailAddress = [Username] + '@' + @emailDomain,
                       UnconfirmedEmailAddress = NULL,
                       HashedPassword = CAST(NEWID() AS NVARCHAR(MAX)),
                       EmailAllowed = 1,
                       EmailConfirmationToken = NULL,
                       PasswordResetToken = NULL,
                       PasswordResetTokenExpirationDate = NULL,
                       PasswordHashAlgorithm = 'PBKDF2'
               WHERE   [Key] NOT IN (SELECT ur.UserKey FROM UserRoles ur INNER JOIN Roles r ON r.[Key] = ur.RoleKey WHERE r.Name = 'Admins')";

        private static readonly string[] AllowedPrefixes = new[] {
            "Export_" // Only exports can be sanitized
        };

        [Option("Domain name to use for sanitized email addresses, username@[emaildomain]", AltName = "e")]
        public string EmailDomain { get; set; }

        [Option("Forces the command to run, even against a non-backup/export database", AltName = "f")]
        public bool Force { get; set; }

        public override void ValidateArguments()
        {
            base.ValidateArguments();
            EmailDomain = String.IsNullOrEmpty(EmailDomain) ?
                "example.com" :
                EmailDomain;
        }

        public override void ExecuteCommand()
        {
            // Verify the name
            if (!Force && !AllowedPrefixes.Any(p => ConnectionString.InitialCatalog.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Error("Cannot sanitize database named '{0}' without -Force argument", ConnectionString.InitialCatalog);
                return;
            }
            Log.Info("Ready to sanitize {0} on {1}", ConnectionString.InitialCatalog, Util.GetDatabaseServerName(ConnectionString));

            // All we need to sanitize is the user table. Package data is public (EVEN unlisted ones) and not PII
            if (WhatIf)
            {
                Log.Trace("Would execute the following SQL:");
                Log.Trace(SanitizeUsersQuery);
                Log.Trace("With @emailDomain = " + EmailDomain);
            }
            else
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString.ConnectionString))
                using (SqlExecutor dbExecutor = new SqlExecutor(connection))
                {
                    connection.Open();
                    try
                    {
                        var count = dbExecutor.Execute(SanitizeUsersQuery, new { emailDomain = EmailDomain });
                        Log.Info("Sanitization complete. {0} Users affected", count);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }
            }
        }
    }
}
