// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Ng.Json
{
    public abstract class InterceptingJsonReader
        : JsonReader
    {
        private static readonly ConcurrentDictionary<string, Regex> CachedRegexes = new ConcurrentDictionary<string, Regex>();

        protected JsonReader InnerReader { get; }
        protected List<Regex> PropertiesToIntercept { get; }

        protected InterceptingJsonReader(JsonReader innerReader, IEnumerable<string> interceptPaths)
        {
            InnerReader = innerReader;
            InnerReader.DateParseHandling = DateParseHandling.None;

            PropertiesToIntercept = interceptPaths
                .Distinct(StringComparer.Ordinal)
                .Select(path => MakeRegex(path))
                .ToList();
        }

        private static Regex MakeRegex(string jsonPath)
        {
            Regex expression = null;
            if (!CachedRegexes.TryGetValue(jsonPath, out expression))
            {
                var pattern = jsonPath
                    .Replace("[*]", @"\[\d+\]")
                    .Replace(".", @"\.");

                expression = new Regex("^" + pattern);

                CachedRegexes.TryAdd(jsonPath, expression);
            }
      
            return expression;
        }

        private bool TestPath(string jsonPath)
        {
            foreach (var property in PropertiesToIntercept)
            {
                if (property.IsMatch(jsonPath))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool Read()
        {
            if (!InnerReader.Read())
            {
                return false;
            }

            if (InnerReader.TokenType == JsonToken.PropertyName && TestPath(InnerReader.Path))
            {
                return OnReadInterceptedPropertyName();
            }

            return true;
        }

        protected abstract bool OnReadInterceptedPropertyName();

        public override object Value
        {
            get
            {
                return InnerReader.Value;
            }
        }

        public override JsonToken TokenType
        {
            get
            {
                return InnerReader.TokenType;
            }
        }

        public override string Path
        {
            get
            {
                return InnerReader.Path;
            }
        }

        public override int Depth
        {
            get
            {
                return InnerReader.Depth;
            }
        }

        public override Type ValueType
        {
            get
            {
                return InnerReader.ValueType;
            }
        }
    }
}