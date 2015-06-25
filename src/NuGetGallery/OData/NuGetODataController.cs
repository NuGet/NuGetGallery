// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;
using NuGetGallery.Configuration;

namespace NuGetGallery.OData
{
    public abstract class NuGetODataController : ODataController
    {
        private readonly ConfigurationService _configurationService;

        public NuGetODataController(ConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        protected virtual HttpContextBase GetTraditionalHttpContext()
        {
            object context;
            if (Request.Properties.TryGetValue("MS_HttpContext", out context))
            {
                var httpContext = context as HttpContext;
                if (httpContext != null)
                {
                    return new HttpContextWrapper(httpContext);
                }

                var httpContextWrapper = context as HttpContextWrapper;
                return httpContextWrapper;
            }

            return null;
        }

        protected virtual bool UseHttps()
        {
            return Request.RequestUri.Scheme == "https";
        }

        protected virtual string GetSiteRoot()
        {
            return _configurationService.GetSiteRoot(UseHttps()).TrimEnd('/') + '/';
        }

        protected virtual HttpResponseMessage CountResult(long count)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(count.ToString(CultureInfo.InvariantCulture), Encoding.UTF8, "text/plain")
            };
        }
    }
}