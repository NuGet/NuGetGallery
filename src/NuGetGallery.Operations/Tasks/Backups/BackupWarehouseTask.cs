// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations.Tasks.Backups
{
    [Command("backupwarehouse", "Backs up the database", AltName = "bw", MaxArgs = 0)]
    public class BackupWarehouseTask : BackupDatabaseTask
    {
        public override void ValidateArguments()
        {
            BackupNamePrefix = BackupNamePrefix ?? "WarehouseBackup_";

            base.ValidateArguments();
        }

        protected override SqlConnectionStringBuilder SelectEnvironmentConnection(DeploymentEnvironment env)
        {
            return env.WarehouseDatabase;
        }
    }
}
