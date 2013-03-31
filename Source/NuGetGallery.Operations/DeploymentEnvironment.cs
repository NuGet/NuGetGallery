using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.WindowsAzure.Storage;

namespace NuGetGallery.Operations
{
    public class DeploymentEnvironment
    {
        public SqlConnectionStringBuilder MainDatabase { get; private set; }
        public CloudStorageAccount MainStorage { get; private set; }

        public DeploymentEnvironment(IDictionary<string, string> deploymentSettings)
        {
            MainDatabase = new SqlConnectionStringBuilder(deploymentSettings["Gallery.Sql.NuGetGallery"]);
            MainStorage = CloudStorageAccount.Parse(deploymentSettings["Gallery.AzureStorageConnectionString"]);
        }

        public static DeploymentEnvironment FromConfigFile(string configFile)
        {
            // Load the file
            var doc = XDocument.Load(configFile);

            // Build a dictionary of settings
            var settings = BuildSettingsDictionary(doc);

            // Construct the deployment environment
            return new DeploymentEnvironment(settings);
        }

        private static IDictionary<string, string> BuildSettingsDictionary(XDocument doc)
        {
            XNamespace ns = XNamespace.Get("http://schemas.microsoft.com/ServiceHosting/2008/10/ServiceConfiguration");
            return (from s in doc.Element(ns + "ServiceConfiguration")
                        .Element(ns + "Role")
                        .Element(ns + "ConfigurationSettings")
                        .Elements(ns + "Setting")
                    select new KeyValuePair<string, string>(
                        s.Attribute("name").Value,
                        s.Attribute("value").Value))
                    .ToDictionary(p => p.Key, p => p.Value);
        }
    }
}
