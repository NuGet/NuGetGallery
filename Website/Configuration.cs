using System;
using System.Configuration;
using System.Web;

namespace NuGetGallery
{
    public class Configuration : IConfiguration
    {
        public static string ReadFromConfigOrEnvironment(
            string key,
            string defaultValue = null)
        {
            var configKey = "NuGetGallery:" + key;
            var environmentVariableName = "NUGET_GALLERY_" + key.ToUpperInvariant().Replace(":", "_");

            var configValue = ConfigurationManager.AppSettings[configKey];
            var environmentVariableValue = Environment.GetEnvironmentVariable(
                environmentVariableName,
                EnvironmentVariableTarget.Machine);

            return configValue ?? environmentVariableValue ?? defaultValue;
        }

        public string BaseUrl 
        {
            get
            {
                return new Lazy<string>(() =>
                    Configuration.ReadFromConfigOrEnvironment("BaseUrl", "http://localhost"))
                        .Value;
            }
        }

        public string PackageFileDirectory
        {
            get 
            {
                return new Lazy<string>(() =>
                    Configuration.ReadFromConfigOrEnvironment("PackageFileDirectory", HttpContext.Current.Server.MapPath("~/App_Data/Packages"))).Value;
            }
        }
    }
}