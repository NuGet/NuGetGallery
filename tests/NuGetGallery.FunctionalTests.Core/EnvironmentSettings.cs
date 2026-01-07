// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;

namespace NuGetGallery.FunctionalTests
{
    /// <summary>
    /// This class reads the various test run settings which are set through env variable.
    /// </summary>
    public static class EnvironmentSettings
    {
        private static EnvironmentVariableTarget[] Targets = new[]
        {
            EnvironmentVariableTarget.Process,
            EnvironmentVariableTarget.User,
            EnvironmentVariableTarget.Machine
        };

        public const string ConfigurationFilePathVariableName = "ConfigurationFilePath";
        public static string ConfigurationFilePath => GetEnvironmentVariable(ConfigurationFilePathVariableName, required: true);

        private static string GetEnvironmentVariable(string key, bool required)
        {
            var output = Targets
                .Select(target => Environment.GetEnvironmentVariable(key, target))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .FirstOrDefault();

            if (required && output == null)
            {
                throw new ArgumentException($"The environment variable '{key}' is not defined.");
            }

            return output;
        }
    }
}
