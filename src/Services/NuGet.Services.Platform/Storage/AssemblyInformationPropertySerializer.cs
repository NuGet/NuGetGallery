using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace NuGet.Services.Storage
{
    public class AssemblyInformationPropertySerializer : TablePropertySerializer
    {
        public override void Write(string name, object value, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            var asmInfo = value as AssemblyInformation;
            if (value != null)
            {
                properties.Add(name + "_FullName", EntityProperty.CreateEntityPropertyFromObject(asmInfo.FullName.ToString()));
                properties.Add(name + "_BuildBranch", EntityProperty.CreateEntityPropertyFromObject(asmInfo.BuildBranch));
                properties.Add(name + "_BuildCommit", EntityProperty.CreateEntityPropertyFromObject(asmInfo.BuildCommit));
                properties.Add(name + "_BuildDate", EntityProperty.CreateEntityPropertyFromObject(asmInfo.BuildDate.UtcDateTime));

                string repoStr;
                if (asmInfo.SourceCodeRepository.IsAbsoluteUri)
                {
                    repoStr = asmInfo.SourceCodeRepository.AbsoluteUri;
                }
                else
                {
                    repoStr = asmInfo.SourceCodeRepository.ToString();
                }
                properties.Add(name + "_SourceCodeRepository", EntityProperty.CreateEntityPropertyFromObject(repoStr));
            }
        }

        public override object Read(Type targetType, string name, IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if (targetType == typeof(AssemblyInformation))
            {
                string fullName = GetOrDefault<string>(properties, name + "_FullName");
                string buildBranch = GetOrDefault<string>(properties, name + "_BuildBranch");
                string buildCommit = GetOrDefault<string>(properties, name + "_BuildCommit");
                DateTime buildDate = GetOrDefault<DateTime>(properties, name + "_BuildDate");
                string repoStr = GetOrDefault<string>(properties, name + "_SourceCodeRepository");

                Uri repo;
                if (!Uri.TryCreate(repoStr, UriKind.RelativeOrAbsolute, out repo))
                {
                    repo = null;
                }

                AssemblyName asmName = new AssemblyName(fullName);

                return new AssemblyInformation(
                    asmName,
                    buildBranch,
                    buildCommit,
                    new DateTimeOffset(buildDate, TimeSpan.Zero),
                    repo);
            }
            return null;
        }
    }
}
