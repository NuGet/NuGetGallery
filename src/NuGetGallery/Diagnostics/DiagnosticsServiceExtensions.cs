// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Diagnostics
{
    public static class DiagnosticsServiceExtensions
    {
        public static IDiagnosticsSource SafeGetSource(this IDiagnosticsService self, string name)
        {
            // Hyper-defensive code to get a diagnostics source when self could be null AND self.GetSource(name) could return null.
            // Designed to support all kinds of mocking scenarios and basically just never fail :)
            try
            {
                return self == null ?
                    NullDiagnosticsSource.Instance :
                    (self.GetSource(name) ?? NullDiagnosticsSource.Instance);
            }
            catch(Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Error getting trace source: " + ex.ToString());
                return NullDiagnosticsSource.Instance;
            }
        }
    }
}
