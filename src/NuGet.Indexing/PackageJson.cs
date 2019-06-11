// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using NuGet.Services.Entities;
using GalleryPackageDependency = NuGet.Services.Entities.PackageDependency;

namespace NuGet.Indexing
{
    public static class PackageJson
    {
        public static JObject ToJson(Package package)
        {
            JObject obj = new JObject();

            obj.Add("Key", package.Key);
            obj.Add("PackageRegistrationKey", package.PackageRegistrationKey);
            obj.Add("PackageRegistration", ToJson_PackageRegistration(package.PackageRegistration));
            obj.Add("Version", package.Version);
            obj.Add("NormalizedVersion", package.NormalizedVersion);

            obj.Add("Title", package.Title);
            obj.Add("Description", package.Description);
            obj.Add("Summary", package.Summary);
            obj.Add("Authors", package.FlattenedAuthors);
            obj.Add("Copyright", package.Copyright);
            obj.Add("Language", package.Language);
            obj.Add("Tags", package.Tags);
            obj.Add("ReleaseNotes", package.ReleaseNotes);
            obj.Add("ProjectUrl", package.ProjectUrl);
            obj.Add("IconUrl", package.IconUrl);

            obj.Add("IsLatest", package.IsLatest);
            obj.Add("IsLatestStable", package.IsLatestStable);
            obj.Add("Listed", package.Listed);

            obj.Add("Created", package.Created);
            obj.Add("Published", package.Published);
            obj.Add("LastUpdated", package.LastUpdated);
            obj.Add("LastEdited", package.LastEdited);

            obj.Add("DownloadCount", package.DownloadCount);

            obj.Add("FlattenedDependencies", package.FlattenedDependencies);
            obj.Add("Dependencies", ToJson_PackageDependencies(package.Dependencies));
            obj.Add("SupportedFrameworks", ToJson_SupportedFrameworks(package.SupportedFrameworks));
            obj.Add("MinClientVersion", package.MinClientVersion);

            obj.Add("Hash", package.Hash);
            obj.Add("HashAlgorithm", package.HashAlgorithm);
            obj.Add("PackageFileSize", package.PackageFileSize);

            obj.Add("LicenseUrl", package.LicenseUrl);
            obj.Add("RequiresLicenseAcceptance", package.RequiresLicenseAcceptance);
            obj.Add("LicenseNames", package.LicenseNames);
            obj.Add("LicenseReportUrl", package.LicenseReportUrl);
            obj.Add("HideLicenseReport", package.HideLicenseReport);

            return obj;
        }

        private static JObject ToJson_PackageRegistration(PackageRegistration packageRegistration)
        {
            JArray owners = ToJson_Owners(packageRegistration.Owners);

            JObject obj = new JObject();

            obj.Add("Key", packageRegistration.Key);
            obj.Add("Id", packageRegistration.Id);
            obj.Add("DownloadCount", packageRegistration.DownloadCount);
            obj.Add("Owners", owners);

            return obj;
        }

        internal static JArray ToJson_Owners(ICollection<User> owners)
        {
            JArray array = new JArray();
            foreach (User owner in owners)
            {
                array.Add(owner.Username);
            }
            return array;
        }

        internal static JArray ToJson_PackageDependencies(ICollection<GalleryPackageDependency> dependencies)
        {
            JArray array = new JArray();
            foreach (GalleryPackageDependency packageDependency in dependencies)
            {
                JObject obj = new JObject();
                obj.Add("Id", packageDependency.Id);
                obj.Add("VersionSpec", packageDependency.VersionSpec);
                obj.Add("TargetFramework", packageDependency.TargetFramework);
                array.Add(obj);
            }
            return array;
        }

        internal static JArray ToJson_SupportedFrameworks(ICollection<PackageFramework> supportedFrameworks)
        {
            JArray array = new JArray();
            foreach (PackageFramework packageFramework in supportedFrameworks)
            {
                array.Add(packageFramework.TargetFramework);
            }
            return array;
        }

        public static Package FromJson(JObject obj)
        {
            Package package = new Package();

            package.Key = obj["Key"].ToObject<int>();
            package.PackageRegistrationKey = obj["PackageRegistrationKey"].ToObject<int>();
            package.PackageRegistration = FromJson_PackageRegistration((JObject)obj["PackageRegistration"]);
            package.Version = obj["Version"].ToString();
            package.NormalizedVersion = obj["NormalizedVersion"].ToString();

            package.Title = obj["Title"].ToString();
            package.Description = obj["Description"].ToString();
            package.Summary = obj["Summary"].ToString();
            package.FlattenedAuthors = obj["Authors"].ToString();
            package.Copyright = obj["Copyright"].ToString();
            package.Language = obj["Language"].ToString();
            package.Tags = obj["Tags"].ToString();
            package.ReleaseNotes = obj["ReleaseNotes"].ToString();
            package.ProjectUrl = obj["ProjectUrl"].ToString();
            package.IconUrl = obj["IconUrl"].ToString();

            package.IsLatest = obj["IsLatest"].ToObject<bool>();
            package.IsLatestStable = obj["IsLatestStable"].ToObject<bool>();
            package.Listed = obj["Listed"].ToObject<bool>();

            package.Created = obj["Created"].ToObject<DateTime>();
            package.Published = obj["Published"].ToObject<DateTime>();
            package.LastUpdated = obj["LastUpdated"].ToObject<DateTime>();

            JToken lastEdited = obj["LastEdited"];
            package.LastEdited = (lastEdited.Type == JTokenType.Null) ? (DateTime?)null : lastEdited.ToObject<DateTime>();

            package.DownloadCount = obj["DownloadCount"].ToObject<int>();

            package.FlattenedDependencies = obj["FlattenedDependencies"].ToString();
            package.Dependencies = FromJson_PackageDependencies((JArray)obj["Dependencies"]);
            package.SupportedFrameworks = FromJson_SupportedFrameworks((JArray)obj["SupportedFrameworks"]);
            package.MinClientVersion = obj["MinClientVersion"].ToString();

            package.Hash = obj["Hash"].ToString();
            package.HashAlgorithm = obj["HashAlgorithm"].ToString();
            package.PackageFileSize = obj["PackageRegistrationKey"].ToObject<int>();
            package.RequiresLicenseAcceptance = obj["RequiresLicenseAcceptance"].ToObject<bool>();
            package.LicenseUrl = obj["LicenseUrl"].ToString();
            package.LicenseNames = obj["LicenseNames"].ToString();
            package.LicenseReportUrl = obj["LicenseReportUrl"].ToString();
            package.HideLicenseReport = obj["HideLicenseReport"].ToObject<bool>();

            return package;
        }

        private static ICollection<GalleryPackageDependency> FromJson_PackageDependencies(JArray array)
        {
            List<GalleryPackageDependency> packageDependencies = new List<GalleryPackageDependency>();
            foreach (JObject obj in array)
            {
                GalleryPackageDependency packageDependency = new GalleryPackageDependency();
                packageDependency.Id = obj["Id"].ToString();
                packageDependency.VersionSpec = obj["VersionSpec"].ToString();
                packageDependency.TargetFramework = obj["TargetFramework"].ToString();
                packageDependencies.Add(packageDependency);
            }
            return packageDependencies;
        }

        private static PackageRegistration FromJson_PackageRegistration(JObject obj)
        {
            ICollection<User> owners = FromJson_Owners((JArray)obj["Owners"]);

            PackageRegistration packageRegistration = new PackageRegistration();

            packageRegistration.Id = obj["Id"].ToString();
            packageRegistration.DownloadCount = obj["DownloadCount"].ToObject<int>();
            packageRegistration.Key = obj["Key"].ToObject<int>();
            packageRegistration.Owners = owners;

            return packageRegistration;
        }

        private static ICollection<User> FromJson_Owners(JArray array)
        {
            List<User> owners = new List<User>();
            foreach (JToken token in array)
            {
                User owner = new User();
                owner.Username = token.ToString();
                owners.Add(owner);
            }
            return owners;
        }

        private static ICollection<PackageFramework> FromJson_SupportedFrameworks(JArray array)
        {
            List<PackageFramework> supportedFrameworks = new List<PackageFramework>();
            foreach (JToken token in array)
            {
                PackageFramework supportedFramework = new PackageFramework();
                supportedFramework.TargetFramework = token.ToString();
                supportedFrameworks.Add(supportedFramework);
            }
            return supportedFrameworks;
        }
    }
}
