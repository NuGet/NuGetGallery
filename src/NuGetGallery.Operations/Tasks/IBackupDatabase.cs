// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery.Operations
{
    // this interface allows backup Jobs to combine Backup Task with Check Status tasks
    public interface IBackupDatabase
    {
        SqlConnectionStringBuilder ConnectionString { get; }
        string BackupName { get; }
        bool SkippingBackup { get; }
    }
}
