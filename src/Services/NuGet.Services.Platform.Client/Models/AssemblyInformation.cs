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

        public AssemblyInformation(AssemblyName fullName, string buildBranch, string buildCommit, string buildDate, string repository)
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
    }
}
