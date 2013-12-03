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

namespace NuGet.Services.Jobs
{
    public class JobDescription : TableEntity
    {
        private Func<JobBase> _constructor;

        public string Name { get; set; }
        public string Description { get; set; }
        public string Runtime { get; set; }
        public Type EventProvider { get; set; }
        public bool? Enabled { get; set; }

        [Obsolete("For serialization only")]
        public JobDescription() { }

        public JobDescription(string name, string runtime, Func<JobBase> constructor)
            : this(name, null, runtime, null, constructor) { }

        public JobDescription(string name, string description, string runtime, Type eventProvider, Func<JobBase> constructor)
        {
            Name = name;
            Description = description;
            Runtime = runtime;
            EventProvider = eventProvider;
            _constructor = constructor;
        }

        public JobBase CreateInstance()
        {
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
                eventProvider: attr.EventProvider, 
                constructor: constructor);
        }
    }
}
