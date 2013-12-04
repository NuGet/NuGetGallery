using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace NuGet.Services
{
    public abstract class NuGetServiceHost
    {
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();
        private List<NuGetService> _services = new List<NuGetService>();

        public abstract string Name { get; }
        public abstract ServiceConfiguration Configuration { get; }
        public CancellationToken ShutdownToken { get { return _shutdownTokenSource.Token; } }

        public IReadOnlyList<NuGetService> Services { get { return _services.AsReadOnly(); } }

        public void Shutdown()
        {
            ServicePlatformEventSource.Log.Finished(Name);
            ServicePlatformEventSource.Log.Stopping(Name);
            _shutdownTokenSource.Cancel();
        }

        public virtual IPEndPoint GetEndpoint(string name)
        {
            return null;
        }

        public virtual void AttachService(NuGetService service)
        {
            _services.Add(service);
        }
    }
}
