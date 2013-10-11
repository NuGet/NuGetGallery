using Newtonsoft.Json.Linq;

namespace NuGetGallery
{
    public static class PackageJson
    {
        public static JObject ToJson(Package package)
        {
            JObject obj = new JObject();

            obj.Add("PackageRegistrationKey", package.PackageRegistrationKey);
            obj.Add("Key", package.Key);

            obj.Add("Id", package.PackageRegistration.Id);
            obj.Add("Version", package.Version);

            string title = string.IsNullOrEmpty(package.Title) ? package.PackageRegistration.Id : package.Title;
            obj.Add("Title", title);

            obj.Add("Description", package.Description);
            obj.Add("Authors", package.FlattenedAuthors);

            obj.Add("IsLatest", package.IsLatest);
            obj.Add("IsLatestStable", package.IsLatestStable);

            JArray owners = new JArray();
            foreach (User owner in package.PackageRegistration.Owners)
            {
                owners.Add(owner.Username);
            }
            obj.Add("Owners", owners);

            obj.Add("IconUrl", package.IconUrl);
            obj.Add("Copyright", package.Copyright);
            obj.Add("Created", package.Created);
            obj.Add("FlattenedDependencies", package.FlattenedDependencies);
            obj.Add("Hash", package.Hash);
            obj.Add("HashAlgorithm", package.HashAlgorithm);
            obj.Add("LastUpdated", package.LastUpdated);
            obj.Add("LastEdited", package.LastEdited);
            obj.Add("Language", package.Language);
            obj.Add("LicenseUrl", package.LicenseUrl);
            obj.Add("MinClientVersion", package.MinClientVersion);
            obj.Add("VersionDownloadCount", package.DownloadCount);
            obj.Add("PackageFileSize", package.PackageFileSize);
            obj.Add("ProjectUrl", package.ProjectUrl);
            obj.Add("Published", package.Published);
            obj.Add("ReleaseNotes", package.ReleaseNotes);
            obj.Add("RequiresLicenseAcceptance", package.RequiresLicenseAcceptance);
            obj.Add("Summary", package.Summary);
            obj.Add("LicenseNames", package.LicenseNames);
            obj.Add("LicenseReportUrl", package.LicenseReportUrl);
            obj.Add("HideLicenseReport", package.HideLicenseReport);
            obj.Add("Tags", package.Tags);

            JArray supportedFrameworks = new JArray();
            foreach (PackageFramework packageFramework in package.SupportedFrameworks)
            {
                supportedFrameworks.Add(packageFramework.TargetFramework);
            }
            obj.Add("SupportedFrameworks", supportedFrameworks);

            return obj;
        }

        public static Package FromJson(JObject json)
        {
            //TODO

            return null;
        }
    }
}
