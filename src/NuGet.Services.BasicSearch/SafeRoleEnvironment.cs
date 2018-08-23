// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.IO;

namespace NuGet.Services.BasicSearch
{
    /// <summary>
    /// A safe wrapper for <see cref="RoleEnvironment"/>.
    /// How this service is deployed (e.g. Cloud Service, Web App) determines whether or not <see cref="RoleEnvironment"/> is accessible.
    /// <see cref="Microsoft.WindowsAzure.ServiceRuntime"/> is loaded from the machine and not from the bin directory, so it may or may not be present on the machine.
    /// </summary>
    internal static class SafeRoleEnvironment
    {
        public static bool TryGetConfigurationSettingValue(string configurationSettingName, out string value)
        {
            return TryGetField(() => RoleEnvironment.GetConfigurationSettingValue(configurationSettingName), out value);
        }

        public static bool TryGetDeploymentId(out string id)
        {
            return TryGetField(() => RoleEnvironment.DeploymentId, out id);
        }

        public static bool TryGetLocalResourceRootPath(string name, out string path)
        {
            return TryGetField(() => RoleEnvironment.GetLocalResource(name).RootPath, out path);
        }

        private static bool _assemblyIsAvailable = true;
        private static bool TryGetField<T>(Func<T> getValue, out T value)
        {
            value = default(T);

            try
            {
                if (!_assemblyIsAvailable || !RoleEnvironment.IsAvailable)
                {
                    // If RoleEnvironment isn't available, we can't access it.
                    return false;
                }

                value = getValue();
                return true;
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException || e is FileLoadException || e is BadImageFormatException)
                {
                    // If an exception related to loading files is thrown, the assembly is not available.
                    // Cache the fact that it is not available so we don't repeatedly throw and catch exceptions.
                    _assemblyIsAvailable = false;
                    return false;
                }
                else
                {
                    // If an exception unrelated to loading files is thrown, the assembly must have thrown the exception itself.
                    // Rethrow the exception so it can be handled by the caller.
                    throw;
                }
            }
        }
    }
}