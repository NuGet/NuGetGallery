using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using NuGet.Services.Entities;
using NuGetGallery.Cookies;
using Microsoft.Extensions.Logging;

namespace NuGetGallery.Services
{
    public class NullTyposquattingService : ITyposquattingService
    {
        public bool IsUploadedPackageIdTyposquatting(
            string uploadedPackageId, 
            User uploadedPackageOwner, 
            IQueryable<PackageRegistration> allPackageRegistrations, 
            int checkListConfiguredLength, 
            TimeSpan checkListExpireTimeInHours, 
            out List<string> typosquattingCheckCollisionIds, 
            out Dictionary<TyposquattingMetric, object> telemetry)
        {
            typosquattingCheckCollisionIds = new List<string>();
            telemetry = new Dictionary<TyposquattingMetric, object>();
            return false;
        }
    }
}