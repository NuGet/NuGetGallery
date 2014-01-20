using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGet.Services.Work
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class JobAttribute : Attribute
    {
        private static readonly Regex NameExtractor = new Regex(@"^(?<shortname>.*)Job$");

        public string Name { get; private set; }
        public Type EventProvider { get; private set; }

        public JobAttribute(string name)
        {
            Name = name;
        }

        public JobAttribute(string name, Type eventProvider)
        {
            Name = name;
            EventProvider = eventProvider;
        }

        public static JobAttribute Get(Type jobType)
        {
            var attr = jobType.GetCustomAttribute<JobAttribute>();
            if (attr != null)
            {
                return attr;
            }
            else
            {
                // Infer an attribute
                string name;
                var match = NameExtractor.Match(jobType.Name);
                if (match.Success)
                {
                    name = match.Groups["shortname"].Value;
                }
                else
                {
                    name = jobType.Name;
                }

                Type jobBase = jobType.BaseType;
                while (jobBase != null && (!jobBase.IsGenericType || (jobBase.GetGenericTypeDefinition() != typeof(JobHandlerBase<>))))
                {
                    jobBase = jobBase.BaseType;
                }
                
                Type eventProvider = null;
                if (jobBase != null) {
                    eventProvider = jobBase.GetGenericArguments()[0];
                }
                return new JobAttribute(name, eventProvider);
            }
        }
    }
}
