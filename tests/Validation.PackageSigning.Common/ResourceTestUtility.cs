// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.Internal.NuGet.Testing.SignedPackages
{
    public static class ResourceTestUtility
    {
        public static string GetResource(string name, Type type)
        {
            using Stream resourceStream = type.Assembly.GetManifestResourceStream(name);
            if (resourceStream is null)
            {
                throw new ArgumentException($"Couldn't find resource '{name}' in assembly {type.Assembly.FullName}");
            }

            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd();
            }
        }

        public static void CopyResourceToFile(string resourceName, Type type, string filePath)
        {
            var bytes = GetResourceBytes(resourceName, type);

            File.WriteAllBytes(filePath, bytes);
        }

        public static byte[] GetResourceBytes(string name, Type type)
        {
            using Stream resourceStream = type.Assembly.GetManifestResourceStream(name);
            if (resourceStream is null)
            {
                throw new ArgumentException($"Couldn't find resource '{name}' in assembly {type.Assembly.FullName}");
            }

            using (var reader = new BinaryReader(resourceStream))
            {
                return reader.ReadBytes((int)reader.BaseStream.Length);
            }
        }
    }
}
