using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NuGetGallery.Diagnostics
{
    public class NullDiagnosticsSource : IDiagnosticsSource
    {
        public static readonly NullDiagnosticsSource Instance = new NullDiagnosticsSource();

        private NullDiagnosticsSource() { }

        public void TraceEvent(System.Diagnostics.TraceEventType type, int id, string message, string member = null, string file = null, int line = 0)
        {
            // No-op!
        }
    }
}
