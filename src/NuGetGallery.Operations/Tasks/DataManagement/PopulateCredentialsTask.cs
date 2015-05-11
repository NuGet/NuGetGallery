// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using NuGetGallery.Operations.Infrastructure;

namespace NuGetGallery.Operations.Tasks.DataManagement
{
    [Command("populatecredentials", "Populates the Credentials table for users who are missing data", AltName = "pc")]
    public class PopulateCredentialsTask : DatabaseTask
    {
        private const string WhatIfQuery = "BEGIN TRAN\r\n" + Query + "\r\nROLLBACK TRAN";
        private const string CommitQuery = "BEGIN TRAN\r\n" + Query + "\r\nCOMMIT TRAN";

        private const string Query = @"
            DECLARE @results TABLE(
                Action nchar(10),
                UserKey int,
                Type nvarchar(64),
                Value nvarchar(256)
            )

            MERGE INTO Credentials dest
            USING @creds src
                ON src.UserKey = dest.UserKey AND src.Type = dest.Type
            WHEN NOT MATCHED THEN 
                INSERT(UserKey, Type, Value)
                VALUES(src.UserKey, src.Type, src.Value)
            OUTPUT 
                $action AS 'Action', 
                inserted.UserKey, 
                inserted.Type, 
                inserted.Value
                INTO @results;

            SELECT COUNT(*) FROM @results
";

        private Dictionary<string, string> _hashAlgorithmToCredType = new Dictionary<string, string>() {
            {Constants.PBKDF2HashAlgorithmId, CredentialTypes.Password.Pbkdf2},
            {Constants.Sha1HashAlgorithmId, CredentialTypes.Password.Sha1}
        };

        public override void ExecuteCommand()
        {
            WithConnection(c =>
            {
                // Get user credentials
                var users = c.Query("SELECT [Key], HashedPassword, PasswordHashAlgorithm, ApiKey FROM Users");

                // Build a table
                var dt = new DataTable();
                dt.Columns.Add("UserKey", typeof(int));
                dt.Columns.Add("Type", typeof(string));
                dt.Columns.Add("Value", typeof(string));
                foreach (var user in users)
                {
                    var row = dt.NewRow();
                    row.SetField("UserKey", (int)user.Key);
                    row.SetField("Type", CredentialTypes.ApiKeyV1);
                    row.SetField("Value", ((Guid)user.ApiKey).ToString().ToLowerInvariant());
                    dt.Rows.Add(row);


                    string passwordCredType;
                    if (!_hashAlgorithmToCredType.TryGetValue(user.PasswordHashAlgorithm, out passwordCredType))
                    {
                        Log.Error("Unknown Hash Algorithm: {0}", user.PasswordHashAlgorithm);
                    }
                    else
                    {
                        row = dt.NewRow();
                        row.SetField("UserKey", (int)user.Key);
                        row.SetField("Type", passwordCredType);
                        row.SetField("Value", (string)user.HashedPassword);
                        dt.Rows.Add(row);
                    }
                }

                WithTableType(c, "Temp_PopulateCredentialsInputType", "UserKey int, Type nvarchar(64), Value nvarchar(256)", () =>
                {
                    // Update the DB
                    var updatedRowCount = c.Execute(
                        WhatIf ? WhatIfQuery : CommitQuery, 
                        new TableValuedParameter("@creds", "Temp_PopulateCredentialsInputType", dt));

                    Log.Info("Inserted {0} credential records", updatedRowCount);
                });
            });
        }
    }
}
