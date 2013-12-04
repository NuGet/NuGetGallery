using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NuGetGallery
{
    public class AssemblyInformation
    {
        public string BuildBranch { get; private set; }
        public string BuildCommit { get; private set; }
        public DateTimeOffset BuildDate { get; private set; }
        public Uri SourceCodeRepository { get; private set; }

        private AssemblyInformation(string buildBranch, string buildCommit, string buildDate, string repository)
        {
            BuildBranch = buildBranch;
            BuildCommit = buildCommit;

            DateTimeOffset date;
            if (DateTimeOffset.TryParse(buildDate, out date))
            {
                BuildDate = date;
            }

            Uri repo;
            if (Uri.TryCreate(repository, UriKind.RelativeOrAbsolute, out repo))
            {
                SourceCodeRepository = repo;
            }
        }

        public AssemblyInformation(string buildBranch, string buildCommit, DateTimeOffset buildDate, Uri repository)
        {
            BuildBranch = buildBranch;
            BuildCommit = buildCommit;
            BuildDate = buildDate;
            SourceCodeRepository = repository;
        }

        public static AssemblyInformation ForAssembly(Assembly asm)
        {
            var metadata = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                .ToDictionary(m => m.Key, m => m.Value);
            return new AssemblyInformation(
                GetOrDefault(metadata, "Branch"),
                GetOrDefault(metadata, "CommitId"),
                GetOrDefault(metadata, "BuildDateUtc"),
                GetOrDefault(metadata, "RepositoryUrl"));
        }

        private static string GetOrDefault(Dictionary<string, string> dict, string key)
        {
            string val;
            if (!dict.TryGetValue(key, out val))
            {
                return String.Empty;
            }
            return val;
        }
    }
}
