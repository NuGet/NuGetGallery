using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Autofac;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging;
using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Formatters;
using Microsoft.WindowsAzure.ServiceRuntime;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.Azure
{
    public class AzureServiceHost : ServiceHost
    {
        private NuGetWorkerRole _worker;
        private ServiceHostDescription _description;

        public override ServiceHostDescription Description
        {
            get { return _description; }
        }

        public AzureServiceHost(NuGetWorkerRole worker)
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            _worker = worker;

            _description = new ServiceHostDescription(
                new ServiceHostName(
                    new DatacenterName(
                        GetConfigurationSetting("Host.Environment"),
                        Int32.Parse(GetConfigurationSetting("Host.Datacenter"))),
                    GetConfigurationSetting("Host.Name")),
                RoleEnvironment.CurrentRoleInstance.Id);
        }

        public override string GetConfigurationSetting(string fullName)
        {
            try
            {
                return RoleEnvironment.GetConfigurationSettingValue(fullName);
            }
            catch
            {
                return base.GetConfigurationSetting(fullName);
            }
        }

        protected override IEnumerable<Type> GetServices()
        {
            return _worker.GetServices();
        }

        protected override void InitializePlatformLogging()
        {
            var logsResource = RoleEnvironment.GetLocalResource("Logs");

            // Initialize core platform logging
            FlatFileLog.CreateListener(
                fileName: Path.Combine(logsResource.RootPath, "Platform.log.json"),
                formatter: new JsonEventTextFormatter(EventTextFormatting.None, dateTimeFormat: "O"),
                isAsync: false)
                .EnableEvents(ServicePlatformEventSource.Log, EventLevel.LogAlways);
        }
    }
}
