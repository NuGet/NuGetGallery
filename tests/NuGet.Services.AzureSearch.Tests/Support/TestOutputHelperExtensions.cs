// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using NuGet.Services.AzureSearch.Support;

namespace Xunit.Abstractions
{
    public static class TestOutputHelperExtensions
    {
        public static RecordingLogger<T> GetLogger<T>(this ITestOutputHelper output)
        {
            var factory = new LoggerFactory().AddXunit(output);
            var inner = factory.CreateLogger<T>();
            return new RecordingLogger<T>(inner);
        }
    }
}
