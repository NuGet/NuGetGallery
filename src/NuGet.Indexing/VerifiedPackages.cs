// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace NuGet.Indexing
{
    public static class VerifiedPackages
    {
        /// <summary>
        /// Load the verified packages auxiliary data.
        /// </summary>
        /// <param name="fileName">The name of the file that contains the auxiliary data</param>
        /// <param name="loader">The loader that should be used to fetch the file's content</param>
        /// <param name="logger">The logger</param>
        /// <returns>A case-insensitive set of all the verified packages</returns>
        public static HashSet<string> Load(string fileName, ILoader loader, FrameworkLogger logger)
        {
            try
            {
                using (var reader = loader.GetReader(fileName))
                {
                    return Parse(reader);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Unable to load {FileName} as deserialization threw: {Exception}", fileName, ex);

                throw;
            }
        }

        /// <summary>
        /// Parse the verified packages from the input.
        /// </summary>
        /// <param name="reader">The reader whose content should be parsed</param>
        /// <returns>A case-insensitive set of all the verified packages</returns>
        public static HashSet<string> Parse(JsonReader reader)
        {
            // The file should contain an array of strings, such as: ["Package1","Package2", ...]
            reader.Read();
            ThrowIfNotExpectedToken(reader, JsonToken.StartArray);

            // Read all of the package ID strings from the JSON array.
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packageId = reader.ReadAsString();

            while (packageId != null)
            {
                // Package IDs strings are likely to be duplicates from previous reloads. We'll reuse the
                // interned strings so that duplicated strings can be garbage collected right away.
                result.Add(String.Intern(packageId));

                packageId = reader.ReadAsString();
            }

            ThrowIfNotExpectedToken(reader, JsonToken.EndArray);

            return result;
        }

        private static void ThrowIfNotExpectedToken(JsonReader reader, JsonToken expected)
        {
            if (reader.TokenType != expected)
            {
                throw new InvalidDataException($"Malformed Verified Packages Auxiliary file - expected '{JsonToken.StartArray}', actual: '{reader.TokenType}'");
            }
        }
    }
}
