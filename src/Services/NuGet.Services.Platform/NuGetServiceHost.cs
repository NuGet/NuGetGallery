using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NuGet.Services
{
    public abstract class NuGetServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();

        public abstract string HostInstanceName { get; }
        public abstract ServiceConfiguration Configuration { get; }
        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public void Shutdown()
        {
            _shutdownTokenSource.Cancel();
        }
    }
}
