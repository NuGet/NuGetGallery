using System;
using System.ComponentModel.Composition;

namespace NuGetGallery.Operations.Worker.Jobs
{
    [Export(typeof(WorkerJob))]
    public class CreateWarehouseReportsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(12);
            }
        }

        public override void RunOnce()
        {
            Logger.Info("Starting create warehouse reports task.");
            new CreateWarehouseReportsTask
            {
                WarehouseConnectionString = Settings.WarehouseConnectionString,
                ReportStorage = Settings.ReportStorage,
                WhatIf = Settings.WhatIf
            }.Execute();
            Logger.Info("Finished create warehouse reports task.");
        }
    }
}
