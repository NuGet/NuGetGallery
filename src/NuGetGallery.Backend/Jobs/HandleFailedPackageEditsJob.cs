using System;
using System.ComponentModel.Composition;
using System.Data.SqlClient;
using NuGetGallery.Operations.Tasks;

namespace NuGetGallery.Backend.Jobs
{
    [Export(typeof(WorkerJob))]
    public class HandleFailedPackageEditsJob : WorkerJob
    {
        public override TimeSpan Period
        {
            get
            {
                return TimeSpan.FromHours(24);
            }
        }

        public override void RunOnce()
        {
            Logger.Trace("Starting HandleFailedPackageEditsJob");
            if (string.IsNullOrEmpty(Settings.SmtpUri))
            {
                Logger.Error("Smtp uri not specified.");
                return;
            }
            string[] parts = Settings.SmtpUri.Split( new char[] {':'});
            if (parts == null || parts.Length != 3)
            {
                Logger.Error("Smtp uri not provided in proper format. Make sure it is provided in the format username:password:emailhost.");
                return;
            }
            ExecuteTask(new HandleFailedPackageEditsTask
            {
                ConnectionString = new SqlConnectionStringBuilder(Settings.MainConnectionString),
                UserAccount = parts[0],
                Password = parts[1],
                EmailHost = parts[2]
            });
        }
    }
}