using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NuGet.Services.ServiceModel
{
    public class ServiceDefinition
    {
        public string Name { get; private set; }
        public Type Type { get; private set; }

        public ServiceDefinition(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        private static readonly Regex NameShortener = new Regex("^(?<shortname>.*)Service$");
        public static ServiceDefinition FromType<T>() where T : NuGetService
        {
            return FromType(typeof(T));
        }
        
        public static ServiceDefinition FromType(Type type)
        {
            // Assert/Throw pairs ensure Debug-mode builds get nice asserts and Release-mode builds get exceptions.
            Debug.Assert(type.IsClass, Strings.ServiceDefinition_TypeMustBeClass);
            if (!type.IsClass) { throw new ArgumentException(Strings.ServiceDefinition_TypeMustBeClass, "type"); }
            Debug.Assert(!type.IsAbstract, Strings.ServiceDefinition_TypeMustBeNonAbstract);
            if (type.IsAbstract) { throw new ArgumentException(Strings.ServiceDefinition_TypeMustBeNonAbstract, "type"); }
            Debug.Assert(typeof(NuGetService).IsAssignableFrom(type), Strings.ServiceDefinition_TypeMustInheritFromNuGetService);
            if (!typeof(NuGetService).IsAssignableFrom(type)) { throw new ArgumentException(Strings.ServiceDefinition_TypeMustInheritFromNuGetService, "type"); }

            string name;
            var serviceAttr = type.GetCustomAttribute<ServiceAttribute>();
            if (serviceAttr != null)
            {
                name = serviceAttr.Name;
            }
            else
            {
                name = type.Name;
                var match = NameShortener.Match(name);
                if (match.Success)
                {
                    name = match.Groups["shortname"].Value;
                }
            }
            return new ServiceDefinition(name, type);
        }
    }
}
