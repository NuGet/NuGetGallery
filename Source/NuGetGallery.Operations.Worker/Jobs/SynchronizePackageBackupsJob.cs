//using System;
//using System.ComponentModel.Composition;

//namespace NuGetGallery.Operations.Worker.Jobs
//{
//    //[Export(typeof(WorkerJob))]
//    public class SynchronizePackageBackupsJob : WorkerJob
//    {
//        public override TimeSpan Period
//        {
//            get
//            {
//                return TimeSpan.FromDays(1);
//            }
//        }

//        public override TimeSpan Offset
//        {
//            get
//            {
//                return TimeSpan.FromMinutes(90);
//            }
//        }

//        public override void RunOnce()
//        {
//            Logger.Info("Starting backup packages task.");
//            new SynchronizePackageBackupsTask
//            {
//                SourceStorage = Settings.,
//                DestinationStorage = Settings.AzureStorage,
//                WhatIf = Settings.WhatIf
//            }.Execute();
//            Logger.Info("Finished backup packages task.");
//        }
//    }
//}
