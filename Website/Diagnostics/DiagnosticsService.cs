using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsService : IDiagnosticsService
    {
        public IDiagnosticsSource GetSource(string name)
        {
            return new TraceDiagnosticsSource(name);
        }
    }
}