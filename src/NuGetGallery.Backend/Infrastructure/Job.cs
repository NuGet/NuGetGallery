using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGetGallery.Backend.Tracing;

namespace NuGetGallery.Backend
{
    public abstract class Job
    {
        private static readonly Regex NameExtractor = new Regex(@"^(?<shortname>.*)Job$");

        public virtual string Name { get; private set; }
        public JobInvocation Invocation { get; protected set; }
        
        protected Job()
        {
            Name = InferName();
        }

        protected Job(string name)
            : this()
        {
            Name = name;
        }

        public virtual JobResult Invoke(JobInvocation invocation)
        {
            // Bind invocation information
            Invocation = invocation;
            BindProperties(invocation.Request.Parameters);

            // Invoke the job
            WorkerEventSource.Log.JobStarted(invocation.Request.Name, invocation.Id);
            JobResult result;
            try
            {
                Execute();
                result = JobResult.Completed();
            }
            catch (Exception ex)
            {
                result = JobResult.Faulted(ex);
            }

            if (result.Status != JobStatus.Faulted)
            {
                WorkerEventSource.Log.JobCompleted(invocation.Request.Name, invocation.Id);
            }
            else
            {
                WorkerEventSource.Log.JobFaulted(invocation.Request.Name, result.Exception, invocation.Id);
            }

            // Return the result
            return result;
        }

        public abstract EventSource GetLog();

        protected internal abstract void Execute();

        protected virtual void BindProperties(Dictionary<string, string> dictionary)
        {
            // PERF: Possible optimization is to build a Dynamic method using Expressions that takes a dictionary and does the following for each property
            //  IF dictionary contains property name THEN convert and set value without using reflection
            foreach (var prop in GetBindableProperties().Cast<PropertyDescriptor>())
            {
                string value;
                if (dictionary.TryGetValue(prop.Name, out value))
                {
                    BindProperty(prop, value);
                }
            }
        }

        protected virtual PropertyDescriptorCollection GetBindableProperties()
        {
            return TypeDescriptor.GetProperties(this);
        }

        protected virtual void BindProperty(PropertyDescriptor prop, string value)
        {
            object convertedValue = ConvertPropertyValue(prop, value);
            prop.SetValue(this, convertedValue);
        }

        protected virtual object ConvertPropertyValue(PropertyDescriptor prop, string value)
        {
            return prop.Converter.ConvertFromString(value);
        }

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
