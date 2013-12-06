using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Http;
using NuGet.Services.Storage;

namespace NuGet.Services.Jobs.Api.Controllers
{
    public abstract class NuGetApiController : ApiController
    {
        public NuGetService Service
        {
            get { return Configuration.Services.GetService(typeof(NuGetService)) as NuGetService; }
        }

        public StorageAccountHub PrimaryStorage
        {
            get { return Service == null ? null : Service.Storage.Primary; }
        }
    }
}
