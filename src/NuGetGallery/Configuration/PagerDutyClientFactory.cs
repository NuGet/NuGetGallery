using NuGetGallery.Areas.Admin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace NuGetGallery.Configuration
{
    internal static class PagerDutyClientFactory
    {
        public static async Task<PagerDutyClient> Create(IGalleryConfigurationService configService)
        {
            var currentConfig = await configService.GetCurrent();

            return new PagerDutyClient(currentConfig.PagerDutyAccountName, currentConfig.PagerDutyAPIKey, currentConfig.PagerDutyServiceKey);
        }
    }
}