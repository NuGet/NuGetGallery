using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet;

namespace NuGetGallery.Diagnostics
{
    public interface IDiagnosticsService
    {
        void Error(string format, params object[] args);
        void Info(string format, params object[] args);
        void Warn(string format, params object[] args);
        void Indent();
        void Unindent();
    }

    public static class DiagnosticsServiceExtensions
    {
        public static IDisposable Operation(this IDiagnosticsService self, string title)
        {
            self.Info("+ Operation: {0}", title);
            self.Indent();
            return new DisposableAction(() => self.Unindent());
        }

        public static void Error(this IDiagnosticsService self, Exception ex, string context)
        {
            self.Error("{0}: {1}", context, ex);
        }
    }
}
