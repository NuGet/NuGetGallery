using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NuGet.Services.Operations.Model;
using NuGet.Services.Operations.Serialization;

namespace NuGet.Services.Operations
{
    public class OperationsSession
    {
        private DirectoryInfo _opsFolder;

        public NuOpsEnvironment CurrentEnvironment { get; private set; }
        public IDictionary<string, NuOpsEnvironment> Environments { get; private set; }
        public IDictionary<string, Subscription> Subscriptions { get; private set; }

        public OperationsSession(string opsDefinitionFolder)
        {
            _opsFolder = new DirectoryInfo(opsDefinitionFolder);
            if (!_opsFolder.Exists)
            {
                throw new DirectoryNotFoundException("Operations Definition Folder not found: " + opsDefinitionFolder);
            }

            LoadOpsData();
        }

        private void LoadOpsData()
        {
            string environmentsFile = Path.Combine(_opsFolder.FullName, "Environments.v3.xml");
            if (!File.Exists(environmentsFile))
            {
                throw new FileNotFoundException("Environments Definition file not found: " + environmentsFile);
            }

            var environments = XmlEnvironmentParser.LoadEnvironments(environmentsFile);

            string subscriptionsFile = Path.Combine(_opsFolder.FullName, "Subscriptions.xml");
            if (File.Exists(subscriptionsFile))
            {
                var subscriptions = XmlSubscriptionParser.LoadSubscriptions(subscriptionsFile);
                Subscriptions = subscriptions
                    .GroupBy(s => s.Name)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                ResolveSubscriptions(environments);
            }

            Environments = environments.GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        private void ResolveSubscriptions(IList<NuOpsEnvironment> environments)
        {
            var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            try
            {
                foreach (var environment in environments)
                {
                    Subscription sub;
                    if (environment.Subscription != null && Subscriptions.TryGetValue(environment.Subscription.Name, out sub))
                    {
                        environment.Subscription = sub;

                        // Search for the certificate
                        var match = store.Certificates.Find(
                            X509FindType.FindBySubjectName, "Azure-" + sub.Name.Replace(" ", ""), validOnly: false)
                            .Cast<X509Certificate2>()
                            .FirstOrDefault();
                        environment.Subscription.ManagementCertificate = match;
                    }
                }
            }
            finally
            {
                store.Close();
            }
        }
    }
}
