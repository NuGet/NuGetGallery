// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using NuGetGallery.Operations.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text;

namespace NuGetGallery.Operations
{
    public static class ResourceHelper
    {
        private static Dictionary<Type, ResourceManager> _cachedManagers;

        public static string GetLocalizedString(Type resourceType, string resourceNames)
        {
            if (String.IsNullOrEmpty(resourceNames))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "resourceNames");
            }

            if (resourceType == null)
            {
                throw new ArgumentNullException("resourceType");
            }

            if (_cachedManagers == null)
            {
                _cachedManagers = new Dictionary<Type, ResourceManager>();
            }

            ResourceManager resourceManager;
            if (!_cachedManagers.TryGetValue(resourceType, out resourceManager))
            {
                PropertyInfo property = resourceType.GetProperty("ResourceManager", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

                if (property == null || property.GetGetMethod(nonPublic: true) == null)
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, TaskResources.ResourceTypeDoesNotHaveProperty, resourceType, "ResourceManager"));
                }

                if (property.PropertyType != typeof(ResourceManager))
                {
                    throw new InvalidOperationException(
                        String.Format(CultureInfo.CurrentCulture, TaskResources.ResourcePropertyIncorrectType, resourceNames, resourceType));
                }

                resourceManager = (ResourceManager)property.GetGetMethod(nonPublic: true)
                                                           .Invoke(obj: null, parameters: null);
            }

            var builder = new StringBuilder();
            foreach (var resource in resourceNames.Split(';'))
            {
                string value = resourceManager.GetString(resource);
                if (String.IsNullOrEmpty(value))
                {
                    throw new InvalidOperationException(
                            String.Format(CultureInfo.CurrentCulture, TaskResources.ResourceTypeDoesNotHaveProperty, resourceType, resource));
                }
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }
                builder.Append(value);
            }

            return builder.ToString();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static string GetBatchFromSqlFile(string filename)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename))
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static IEnumerable<string> GetBatchesFromSqlFile(string filename)
        {
            List<string> batches = new List<string>();

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename))
            {
                using (var reader = new StreamReader(stream))
                {
                    StringBuilder batch = new StringBuilder();

                    while (!reader.EndOfStream)
                    {
                        string line = reader.ReadLine();

                        if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                        {
                            batches.Add(batch.ToString());
                            batch.Clear();
                        }
                        else
                        {
                            batch.AppendLine(line);
                        }
                    }
                }
            }

            return batches;
        }
    }
}
