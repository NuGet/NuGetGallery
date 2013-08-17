﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class UpdateLicenseReportsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromMinutes(30);
            }
        }

        public override void RunOnce()
        {
            ExecuteTask(new UpdateLicenseReportsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                WhatIf = Settings.WhatIf,
                LicenseReportService = Settings.LicenseReportService,
                LicenseReportCredentials = Settings.LicenseReportCredentials
            });
        }
    }
}