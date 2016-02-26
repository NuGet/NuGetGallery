// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Ng.Json
{
    public class RegistrationBaseUrlRewritingJsonReader
        : InterceptingJsonReader
    {
        private readonly List<KeyValuePair<string, string>> _replacements;

        private static readonly string[] RegistrationPathsToIntercept = new[]
        {
            // Replace in index.json + {version}.json
            "@id",

            // Replace in index.json:
            "registration",
            
            // Replace in {version}.json:
            "items[*].@id",     

            // Replace in {version}.json:
            "items[*].parent", 

            // Replace in {version}.json:
            "items[*].items[*].@id",  

            // Replace in {version}.json:
            "items[*].items[*].registration",
            
            // Replace in {version}.json:
            "items[*].items[*].catalogEntry.dependencyGroups[*].dependencies[*].registration"
        };
        
        public RegistrationBaseUrlRewritingJsonReader(JTokenReader innerReader, List<KeyValuePair<string, string>> replacements) 
            : base(innerReader, RegistrationPathsToIntercept)
        {
            _replacements = replacements;
        }

        protected override bool OnReadInterceptedPropertyName()
        {
            var tokenReader = InnerReader as JTokenReader;
            var currentProperty = tokenReader?.CurrentToken as JProperty;
            if (currentProperty != null)
            {
                var propertyValue = (string)currentProperty.Value;

                // Run replacements
                foreach (var replacement in _replacements)
                {
                    propertyValue = propertyValue.Replace(replacement.Key, replacement.Value);
                }

                // Set new property value
                currentProperty.Value.Replace(propertyValue);
            }

            return true;
        }
    }
}