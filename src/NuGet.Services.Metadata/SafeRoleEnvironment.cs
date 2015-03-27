using System;
using System.IO;
using System.Reflection;

namespace NuGet.Services.Metadata
{
    /// <summary>
    /// Safe role environment which allows this application to be run on both Azure Cloud Services and Azure Web Sites.
    /// </summary>
    internal static class SafeRoleEnvironment
    {
        private const string _serviceRuntimeAssembly = "Microsoft.WindowsAzure.ServiceRuntime";
        private const string _roleEnvironmentTypeName = "Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment";
        private const string _isAvailablePropertyName = "IsAvailable";

        public static bool IsAvailable { get; private set; }

        static SafeRoleEnvironment()
        {
            // Find out if the code is running in the cloud service context.
            Assembly assembly = GetServiceRuntimeAssembly();
            if (assembly != null)
            {
                Type roleEnvironmentType = assembly.GetType(_roleEnvironmentTypeName, false);
                if (roleEnvironmentType != null)
                {
                    PropertyInfo isAvailableProperty = roleEnvironmentType.GetProperty(_isAvailablePropertyName);

                    try
                    {
                        IsAvailable = isAvailableProperty != null && (bool)isAvailableProperty.GetValue(null, new object[] { });
                    }
                    catch (TargetInvocationException e)
                    {
                        IsAvailable = false;
                    }
                }
            }
        }

        /// <summary>
        /// Loads and returns the latest available version of the service runtime assembly.
        /// </summary>
        /// <returns>Loaded assembly, if any.</returns>
        private static Assembly GetServiceRuntimeAssembly()
        {
            Assembly assembly = null;

            try
            {
                assembly = Assembly.LoadWithPartialName(_serviceRuntimeAssembly);
            }
            catch (Exception e)
            {
                if (!(e is FileNotFoundException || e is FileLoadException || e is BadImageFormatException))
                {
                    throw;
                }
            }

            return assembly;
        }
    }
}