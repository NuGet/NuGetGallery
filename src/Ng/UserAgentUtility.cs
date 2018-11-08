// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;

namespace Ng
{
    public class UserAgentUtility
    {
        /// <summary>
        /// Returns a user agent built using the entry assembly's name and version.
        /// </summary>
        public static string GetUserAgent()
        {
            var assembly = Assembly.GetEntryAssembly();
            var assemblyName = assembly.GetName().Name;
            var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

            return $"{assemblyName}/{assemblyVersion}";
        }
    }
}
