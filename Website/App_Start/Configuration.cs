using System;
using System.Configuration;
using System.Web;

namespace NuGetGallery {
    public class Configuration : IConfiguration {
        static readonly Lazy<string> galleryOwnerEmail =
            new Lazy<string>(() =>
                Configuration.ReadFromConfigOrEnvironment("GalleryOwnerEmail"));
        static readonly Lazy<string> packageFileDirectory =
            new Lazy<string>(() =>
                Configuration.ReadFromConfigOrEnvironment("PackageFileDirectory",
                                                          HttpContext.Current.Server.MapPath("~/App_Data/Packages")));

        public static string ReadFromConfigOrEnvironment(
            string key,
            string defaultValue = null) {
            var configKey = "NuGetGallery:" + key;
            var environmentVariableName = "NUGET_GALLERY_" + key.ToUpperInvariant().Replace(":", "_");

            var configValue = ConfigurationManager.AppSettings[configKey];
            var environmentVariableValue = Environment.GetEnvironmentVariable(
                environmentVariableName,
                EnvironmentVariableTarget.Machine);

            return configValue ?? environmentVariableValue ?? defaultValue;
        }

        public string PackageFileDirectory {
            get {
                return packageFileDirectory.Value;
            }
        }

        public string GalleryOwnerEmail {
            get {
                return galleryOwnerEmail.Value;
            }
        }
    }
}