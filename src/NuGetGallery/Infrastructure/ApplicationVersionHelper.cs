using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Web;
using NuGet;

namespace NuGetGallery
{
    public class ApplicationVersion
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes", Justification = "The type is immutable")]
        public static readonly ApplicationVersion Empty = new ApplicationVersion();

        public bool Present { get; private set; }
        public string Version { get; private set; }
        public string Branch { get; private set; }
        public string Commit { get; private set; }
        public DateTime BuildDateUtc { get; private set; }

        // Calculated
        public string ShortCommit { get; private set; }
        public Uri BranchUri { get; private set; }
        public Uri CommitUri { get; private set; }

        private ApplicationVersion()
        {
            Present = false;
            Version = "1.0";
            Branch = String.Empty;
            Commit = String.Empty;
            BuildDateUtc = DateTime.UtcNow;
        }

        public ApplicationVersion(Uri repositoryBase, string version, string branch, string commit, DateTime buildDateUtc)
        {
            Present = true;
            Version = version;
            Branch = branch;
            Commit = commit;
            BuildDateUtc = buildDateUtc;

            ShortCommit = String.IsNullOrEmpty(Commit) ? String.Empty : Commit.Substring(0, Math.Min(10, Commit.Length));

            if (repositoryBase != null)
            {
                BranchUri = CombineUri(repositoryBase, "branches/" + branch);
                CommitUri = CombineUri(repositoryBase, "commit/" + ShortCommit);
            }
        }

        private static Uri CombineUri(Uri repositoryBase, string relative)
        {
            UriBuilder builder = new UriBuilder(repositoryBase);
            if (String.IsNullOrEmpty(builder.Path))
            {
                builder.Path = relative;
            }
            else
            {
                if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
                {
                    builder.Path += "/";
                }
                builder.Path += relative;
            }
            return builder.Uri;
        }
    }

    public static class ApplicationVersionHelper
    {
        private static readonly Lazy<ApplicationVersion> _version = new Lazy<ApplicationVersion>(LoadVersion);

        public static ApplicationVersion GetVersion()
        {
            return _version.Value;
        }

        private static ApplicationVersion LoadVersion()
        {
            try
            {
                var metadata = typeof(ApplicationVersionHelper)
                    .Assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .ToDictionary(a => a.Key, a => a.Value, StringComparer.OrdinalIgnoreCase);
                var infoVer = typeof(ApplicationVersionHelper)
                    .Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                string ver = infoVer == null ?
                    typeof(ApplicationVersionHelper).Assembly.GetName().Version.ToString() :
                    infoVer.InformationalVersion;

                string branch = TryGet(metadata, "Branch");
                string commit = TryGet(metadata, "CommitId");
                string dateString = TryGet(metadata, "BuildDateUtc");
                string repoUriString = TryGet(metadata, "RepositoryUrl");

                DateTime buildDate;
                if (!DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out buildDate))
                {
                    buildDate = DateTime.MinValue;
                }
                Uri repoUri;
                if (!Uri.TryCreate(repoUriString, UriKind.Absolute, out repoUri))
                {
                    repoUri = null;
                }
                return new ApplicationVersion(
                    repoUri,
                    ver,
                    branch,
                    commit,
                    buildDate);
            }
            catch (Exception ex)
            {
                QuietLog.LogHandledException(ex);
                return ApplicationVersion.Empty;
            }
        }

        private static string TryGet(Dictionary<string, string> metadata, string key)
        {
            string val;
            if (!metadata.TryGetValue(key, out val))
            {
                return String.Empty;
            }
            return val;
        }
    }
}