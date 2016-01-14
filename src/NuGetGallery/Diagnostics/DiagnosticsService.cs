// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Diagnostics;
using System.Globalization;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsService : IDiagnosticsService
    {
        public DiagnosticsService()
        {
            Trace.AutoFlush = true;
        }

        public IDiagnosticsSource GetSource(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, "name"), nameof(name));
            }

            return new TraceDiagnosticsSource(name);
        }
    }
}