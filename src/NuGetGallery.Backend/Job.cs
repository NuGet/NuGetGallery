using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NuGetGallery.Backend.Worker
{
    [InheritedExport]
    public abstract class Job
    {
        private static readonly Regex NameExtractor = new Regex(@"^(?<shortname>.*)Job$");

        public virtual string Name { get; private set; }

        protected Job()
        {
            Name = InferName();
        }

        protected Job(string name) : this()
        {
            Name = name;
        }

        public virtual JobResult Execute(JobInvocation invocation)
        {
            // TODO:
            //  0. Set up ETL trace
            //  1. Bind JobInvocation data to properties on the job
            //  2. Execute the job
            //  3. Queue Async Completion check if necessary
            //  4. Return status
        }

        protected abstract JobResult Execute();

        private string InferName()
        {
            var name = GetType().Name;
            var match = NameExtractor.Match(name);
            if (match.Success)
            {
                return match.Groups["shortname"].Value;
            }
            return name;
        }
    }
}
