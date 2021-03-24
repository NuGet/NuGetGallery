// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery
{
    public interface IGlobalMetricPropertyProvider
    {
        /// <summary>
        /// Adds global properties for metrics. If the name clashes with a property provided by an
        /// individual metric, the value provided by that metric will override the value provided by this method.
        /// </summary>
        /// <param name="addProperty">A function implementation should call to add a property to a property bag.</param>
        void AddProperties(Action<string, string> addProperty);
    }
}
