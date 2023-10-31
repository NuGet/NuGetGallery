using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using NuGet.Services.Entities;
using NuGetGallery.Cookies;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client.TelemetryCore.TelemetryClient;

namespace NuGetGallery.Services
{
    public class NullTyposquattingService : ITyposquattingService
    {
        public TyposquattingCheckResult IsUploadedPackageIdTyposquatting(TyposquattingCheckInfo checkInfo)
        {
            return new TyposquattingCheckResult(
                wasUploadBlocked : false,
                typosquattingCheckCollisionIds : Enumerable.Empty<string>(),
                telemetryData : new Dictionary<TyposquattingCheckMetrics, object>());
        }
    }
}