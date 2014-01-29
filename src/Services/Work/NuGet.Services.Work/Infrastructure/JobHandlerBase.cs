using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NuGet.Services.Work.Monitoring;
using System.Diagnostics;
using System.Data.SqlClient;
using System.IO;
using System.Threading;
using Microsoft.WindowsAzure.Storage;

namespace NuGet.Services.Work
{
    public abstract class JobHandlerBase
    {
        public InvocationContext Context { get; protected set; }

        public InvocationState Invocation { get { return Context.Invocation; } }

        public bool WhatIf { get; set; }

        protected string TempDirectory { get; private set; }
        
        public virtual async Task<InvocationResult> Invoke(InvocationContext context)
        {
            InvocationResult result = BindContext(context);
            if (result != null)
            {
                return result;
            }

            // Invoke the job
            try
            {
                result = await Invoke();
            }
            catch (Exception ex)
            {
                result = InvocationResult.Faulted(ex);
            }

            // Return the result
            return result;
        }

        public abstract EventSource GetEventSource();
        protected internal abstract Task<InvocationResult> Invoke();

        public virtual Task<InvocationState> Enqueue(Type job)
        {
            return Enqueue(JobAttribute.Get(job).Name, new Dictionary<string, string>(), TimeSpan.Zero);
        }

        public virtual Task<InvocationState> Enqueue(string job)
        {
            return Enqueue(job, new Dictionary<string, string>(), TimeSpan.Zero);
        }

        public virtual Task<InvocationState> Enqueue(Type job, Dictionary<string, string> payload)
        {
            return Enqueue(JobAttribute.Get(job).Name, payload, TimeSpan.Zero);
        }

        public virtual Task<InvocationState> Enqueue(string job, Dictionary<string, string> payload)
        {
            return Enqueue(job, payload, TimeSpan.Zero);
        }

        public virtual Task<InvocationState> Enqueue(Type job, Dictionary<string, string> payload, TimeSpan delayFor)
        {
            return Enqueue(JobAttribute.Get(job).Name, payload, delayFor);
        }

        public virtual Task<InvocationState> Enqueue(string job, Dictionary<string, string> payload, TimeSpan delayFor)
        {
            string source = Context.Invocation.Id.ToString("N");
            return Context.Queue.Enqueue(job, source, payload, delayFor);
        }

        protected virtual Task<bool> Extend(TimeSpan duration)
        {
            InvocationEventSource.Log.Extending(DateTimeOffset.UtcNow + duration);
            return Context.Queue.Extend(Invocation, duration);
        }

        protected virtual InvocationResult BindContext(InvocationContext context)
        {
            // Bind invocation information
            Context = context;
            TempDirectory = Path.Combine(
                Path.GetTempPath(),
                "NuGetService",
                "InvocationTemp",
                context.Invocation.Id.ToString("N").ToLowerInvariant());
            try
            {
                BindProperties(Invocation.Payload);
            }
            catch (Exception ex)
            {
                InvocationEventSource.Log.BindingError(ex);
                return InvocationResult.Faulted(ex);
            }
            return null;
        }

        protected virtual void BindProperties(Dictionary<string, string> dictionary)
        {
            // PERF: Possible optimization is to build a DynamicMethod using Expressions that takes a dictionary and does the following for each property
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

        private IList<TypeConverter> _converters = new List<TypeConverter>() {
            new SqlConnectionStringBuilderConverter(),
            new CloudStorageAccountConverter()
        };

        protected virtual object ConvertPropertyValue(PropertyDescriptor prop, string value)
        {
            var converter = _converters.FirstOrDefault(c => c.CanConvertFrom(typeof(string)) && c.CanConvertTo(prop.PropertyType));
            if (converter != null)
            {
                return converter.ConvertFromString(value);
            }
            return prop.Converter.ConvertFromString(value);
        }

        private class SqlConnectionStringBuilderConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string);
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return destinationType == typeof(SqlConnectionStringBuilder);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                string strVal = value as string;
                if (strVal == null)
                {
                    return null;
                }
                return new SqlConnectionStringBuilder(strVal);
            }
        }

        private class CloudStorageAccountConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return sourceType == typeof(string);
            }

            public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
            {
                return destinationType == typeof(CloudStorageAccount);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                string strVal = value as string;
                if (strVal == null)
                {
                    return null;
                }
                return CloudStorageAccount.Parse(strVal);
            }
        }
    }
}
