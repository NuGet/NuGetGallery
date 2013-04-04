using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace NuGetGallery.Diagnostics
{
    public class DiagnosticsService : IDiagnosticsService
    {
        public void Error(string format, params object[] args)
        {
            Trace.TraceError(format, args);
        }

        public void Info(string format, params object[] args)
        {
            Trace.TraceInformation(format, args);
        }

        public void Warn(string format, params object[] args)
        {
            Trace.TraceWarning(format, args);
        }

        public void Indent()
        {
            Trace.Indent();
        }

        public void Unindent()
        {
            Trace.Unindent();
        }
    }
}