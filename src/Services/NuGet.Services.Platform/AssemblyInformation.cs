using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGet.Services.Storage;

namespace NuGet.Services
{
    [PropertySerializer(typeof(AssemblyInformationPropertySerializer))]
    public class AssemblyInformation
    {
        [JsonConverter(typeof(AssemblyFullNameConverter))]
        public AssemblyName FullName { get; private set; }
        public string BuildBranch { get; private set; }
        public string BuildCommit { get; private set; }
        public DateTimeOffset BuildDate { get; private set; }
        public Uri SourceCodeRepository { get; private set; }

        private AssemblyInformation(AssemblyName fullName, string buildBranch, string buildCommit, string buildDate, string repository)
        {
            FullName = fullName;
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

        public AssemblyInformation(AssemblyName fullName, string buildBranch, string buildCommit, DateTimeOffset buildDate, Uri repository)
            : this(buildBranch, buildCommit, buildDate, repository)
        {
            FullName = fullName;
        }
        public AssemblyInformation(string buildBranch, string buildCommit, DateTimeOffset buildDate, Uri repository)
        {
            BuildBranch = buildBranch;
            BuildCommit = buildCommit;
            BuildDate = buildDate;
            SourceCodeRepository = repository;
        }

        public static AssemblyInformation FromObject(object obj)
        {
            return FromAssembly(obj.GetType().Assembly);
        }

        public static AssemblyInformation FromType(Type typ)
        {
            return FromAssembly(typ.Assembly);
        }

        public static AssemblyInformation FromType<T>()
        {
            return FromAssembly(typeof(T).Assembly);
        }

        public static AssemblyInformation FromAssembly(Assembly asm)
        {
            var metadata = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                .ToDictionary(m => m.Key, m => m.Value);
            return new AssemblyInformation(
                asm.GetName(),
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
