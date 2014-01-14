using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.Storage;
using NuGet.Services.Work.Monitoring;

namespace NuGet.Services.Work.Monitoring
{
    public class InvocationLogCaptureFactory
    {
        public virtual InvocationLogCapture CreateCapture(InvocationState invocation)
        {
            return new InvocationLogCapture(invocation);
        }
    }

    public class BlobInvocationLogCaptureFactory : InvocationLogCaptureFactory
    {
        private StorageHub _storage;

        public BlobInvocationLogCaptureFactory(StorageHub storage)
        {
            _storage = storage;
        }

        public override InvocationLogCapture CreateCapture(InvocationState invocation)
        {
            return new BlobInvocationLogCapture(invocation, _storage);
        }
    }
}
