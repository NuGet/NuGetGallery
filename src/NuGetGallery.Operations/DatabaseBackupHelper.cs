// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetGallery.Operations
{
    public static class DatabaseBackupHelper
    {
        public static bool GetBackupStatus(NLog.Logger log, SqlConnectionStringBuilder connectionString, string backupName)
        {
            CheckDatabaseStatusTask checkDatabaseStatusTask = new CheckDatabaseStatusTask
            {
                ConnectionString = connectionString,
                BackupName = backupName,
                WhatIf = false // WhatIf isn't used by this task.
            };

            checkDatabaseStatusTask.Execute();

            if (checkDatabaseStatusTask.State == 0)
            {
                log.Info("Copy of {0} to {1} complete!", connectionString.InitialCatalog, backupName);
            }

            return checkDatabaseStatusTask.State == 0;
        }
    }
}
