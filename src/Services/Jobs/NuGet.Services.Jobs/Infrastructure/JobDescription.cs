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
using NuGetGallery.Storage;
using System.Diagnostics.Tracing;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuGet.Services.Jobs
{
    [Table("Jobs")]
    public class JobDescription : AzureTableEntity
    {
        private Func<JobBase> _constructor;

        public string Name { get; set; }
        public string Description { get; set; }
        public string Runtime { get; set; }
        public Guid? EventProviderId { get; set; }
        public bool? Enabled { get; set; }

        [Obsolete("For serialization only")]
        public JobDescription() { }

        public JobDescription(string name, string runtime, Func<JobBase> constructor)
            : this(name, null, runtime, null, constructor) { }

        public JobDescription(string name, string description, string runtime, Guid? eventProviderId, Func<JobBase> constructor)
        {
            Name = name;
            Description = description;
            Runtime = runtime;
            EventProviderId = eventProviderId;
            _constructor = constructor;
        }

        public JobBase CreateInstance()
        {
            if (_constructor == null)
            {
                throw new InvalidOperationException(Strings.JobDescription_CannotBeConstructed);
            }
            return _constructor();
        }

        public static JobDescription Create(Type jobType)
        {
            var attr = JobAttribute.Get(jobType);
            var descAttr = jobType.GetCustomAttribute<DescriptionAttribute>();
            
            var ctor = jobType.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                // Cannot construct job
                return null;
            }
            var constructor = Expression.Lambda<Func<JobBase>>(Expression.New(ctor)).Compile();
            return new JobDescription(
                name: attr.Name, 
                description: descAttr == null ? null : descAttr.Description,
                runtime: jobType.AssemblyQualifiedName, 
                eventProviderId: attr.EventProvider == null ? (Guid?)null : (Guid?)EventSource.GetGuid(attr.EventProvider), 
                constructor: constructor);
        }
    }
}
