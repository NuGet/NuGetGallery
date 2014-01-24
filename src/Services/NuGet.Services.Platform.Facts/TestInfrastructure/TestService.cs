using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceModel;

namespace NuGet.Services.TestInfrastructure
{
    public class TestService : NuGetService
    {
        public bool WasRun { get; private set; }
        public bool WasStarted { get; private set; }
        public bool WasShutdown { get; private set; }

        public Func<Task> CustomOnRun { get; set; }
        
        public TestService(ServiceName name, ServiceHost host) : base(name, host) { }

        protected override Task OnRun()
        {
            WasRun = true;

            if (CustomOnRun != null)
            {
                return CustomOnRun();
            }
            else
            {
                TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
                tcs.TrySetResult(null);
                return tcs.Task;
            }
        }

        protected override Task<bool> OnStart()
        {
            WasStarted = true;
            return base.OnStart();
        }

        protected override void OnShutdown()
        {
            WasShutdown = true;
            base.OnShutdown();
        }
    }
}
