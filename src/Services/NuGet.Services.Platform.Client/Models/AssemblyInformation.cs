using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NuGet.Services
{
    public class AssemblyInformation
    {
        [JsonConverter(typeof(AssemblyFullNameConverter))]
        public AssemblyName FullName { get; private set; }
        public string BuildBranch { get; private set; }
        public string BuildCommit { get; private set; }
        public DateTimeOffset BuildDate { get; private set; }
        public Uri SourceCodeRepository { get; private set; }

        [JsonConstructor]
        public AssemblyInformation(string fullName, string buildBranch, string buildCommit, string buildDate, string sourceCodeRepository)
            : this(new AssemblyName(fullName), buildBranch, buildCommit, buildDate, sourceCodeRepository)
        {
        }

        public AssemblyInformation(AssemblyName fullName, string buildBranch, string buildCommit, string buildDate, string sourceCodeRepository)
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
            if (Uri.TryCreate(sourceCodeRepository, UriKind.RelativeOrAbsolute, out repo))
            {
                SourceCodeRepository = repo;
            }
        }

        public AssemblyInformation(AssemblyName fullName, string buildBranch, string buildCommit, DateTimeOffset buildDate, Uri sourceCodeRepository)
            : this(buildBranch, buildCommit, buildDate, sourceCodeRepository)
        {
            FullName = fullName;
        }

        public AssemblyInformation(string buildBranch, string buildCommit, DateTimeOffset buildDate, Uri sourceCodeRepository)
        {
            BuildBranch = buildBranch;
            BuildCommit = buildCommit;
            BuildDate = buildDate;
            SourceCodeRepository = sourceCodeRepository;
        }
    }
}
