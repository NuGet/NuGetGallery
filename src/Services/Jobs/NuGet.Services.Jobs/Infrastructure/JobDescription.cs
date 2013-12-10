using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using System.ComponentModel;
using Microsoft.WindowsAzure.Storage.Table;
using System.Diagnostics.Tracing;
using System.ComponentModel.DataAnnotations.Schema;
using NuGet.Services.Storage;
using Autofac.Builder;

namespace NuGet.Services.Jobs
{
    [Table("Jobs")]
    public class JobDescription : AzureTableEntity
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Runtime { get; set; }
        public Guid? EventProviderId { get; set; }
        public bool? Enabled { get; set; }

        [IgnoreProperty]
        public Type Type { get; private set; }

        [Obsolete("For serialization only")]
        public JobDescription() { }

        public JobDescription(string name, string runtime)
            : this(name, null, runtime, null) { }

        public JobDescription(string name, string description, string runtime, Guid? eventProviderId)
        {
            Name = name;
            Description = description;
            Runtime = runtime;
            EventProviderId = eventProviderId;
        }

        public static JobDescription Create(Type jobType)
        {
            var attr = JobAttribute.Get(jobType);
            var descAttr = jobType.GetCustomAttribute<DescriptionAttribute>();

            return new JobDescription(
                name: attr.Name,
                description: descAttr == null ? null : descAttr.Description,
                runtime: jobType.AssemblyQualifiedName,
                eventProviderId: attr.EventProvider == null ? (Guid?)null : (Guid?)EventSource.GetGuid(attr.EventProvider))
                {
                    Type = jobType
                };
        }
    }
}
