using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsService
    {
        IDisposable Time(string title, string subTitle);
    }

    public static class DiagnosticsServiceExtensions
    {
        public static IDisposable Time(this IDiagnosticsService self, string title)
        {
            return self.Time(title, subTitle: null);
        }
    }
}