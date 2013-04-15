using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Web;

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
                throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, Strings.ParameterCannotBeNullOrEmpty, "name"), "name");
            }
            return new TraceDiagnosticsSource(name);
        }
    }
}