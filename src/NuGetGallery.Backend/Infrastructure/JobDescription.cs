using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace NuGetGallery.Backend
{
    public class JobDescription
    {
        private static readonly Regex NameExtractor = new Regex(@"^(?<shortname>.*)Job$");

        private Func<JobBase> _constructor;

        public string Name { get; private set; }
        public string Runtime { get; private set; }

        public JobDescription(string name, string runtime, Func<JobBase> constructor)
        {
            Name = name;
            Runtime = runtime;
            _constructor = constructor;
        }

        public JobBase CreateInstance()
        {
            return _constructor();
        }

        public static JobDescription Create(Type jobType)
        {
            string name;
            var attr = jobType.GetCustomAttribute<JobAttribute>();
            if (attr != null)
            {
                name = attr.Name;
            }
            else
            {
                var match = NameExtractor.Match(jobType.Name);
                if (match.Success)
                {
                    name = match.Groups["shortname"].Value;
                }
                else
                {
                    name = jobType.Name;
                }
            }

            var ctor = jobType.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                // Cannot construct job
                return null;
            }
            var constructor = Expression.Lambda<Func<JobBase>>(Expression.New(ctor)).Compile();
            return new JobDescription(name, jobType.AssemblyQualifiedName, constructor);
        }
    }
}
