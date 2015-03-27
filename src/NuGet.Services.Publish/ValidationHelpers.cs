using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace NuGet.Services.Publish
{
    public class ValidationHelpers
    {
        public static PackageIdentity ValidateIdentity(JObject metadata, IList<string> errors)
        {
            string ns = null;
            string id = null;
            SemanticVersion semanticVersion = null;

            JToken namespaceJToken = CheckRequiredProperty(metadata, errors, "namespace");
            if (namespaceJToken != null)
            {
                ns = namespaceJToken.ToString();
                if (ns.LastIndexOfAny(new[] { '/', '@' }) != -1)
                {
                    errors.Add("'/', '@' characters are not permitted in namespace property");
                }
            }
            else
            {
                ns = Constants.DefaultPackageNamespace;
            }

            JToken idJToken = CheckRequiredProperty(metadata, errors, "id");
            if (idJToken != null)
            {
                id = idJToken.ToString();
                if (id.LastIndexOfAny(new[] { '/', '@' }) != -1)
                {
                    errors.Add("'/', '@' characters are not permitted in id property");
                }
            }

            JToken versionJToken = CheckRequiredProperty(metadata, errors, "version");
            if (versionJToken != null)
            {
                string version = versionJToken.ToString();
                if (!SemanticVersion.TryParse(version, out semanticVersion))
                {
                    errors.Add("the version property must follow the Semantic Version rules, refer to 'http://semver.org'");
                }
            }

            return new PackageIdentity { Namespace = ns, Id = id, Version = semanticVersion };
        }

        public static JToken CheckRequiredProperty(JObject obj, IList<string> errors, string name)
        {
            JToken token;
            if (!obj.TryGetValue(name, out token))
            {
                errors.Add(string.Format("required property '{0}' is missing from 'apiapp.json' file", name));
            }
            return token;
        }

        public static void CheckRequiredFile(Stream packageStream, IList<string> errors, string fullName)
        {
            if (!FileExists(packageStream, fullName))
            {
                errors.Add(string.Format("required file '{0}' was missing from package", fullName));
            }
        }

        static bool FileExists(Stream packageStream, string fullName)
        {
            using (ZipArchive archive = new ZipArchive(packageStream, ZipArchiveMode.Read, true))
            {
                foreach (ZipArchiveEntry zipEntry in archive.Entries)
                {
                    if (zipEntry.FullName == fullName)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}