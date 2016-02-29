// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ng.Json
{
    public class PropertySkippingJsonReader
        : InterceptingJsonReader
    {
        public PropertySkippingJsonReader(JsonReader innerReader, IEnumerable<string> interceptPaths) 
            : base(innerReader, interceptPaths)
        {
        }

        protected override bool OnReadInterceptedPropertyName()
        {
            InnerReader.Skip();

            return Read();
        }
    }
}