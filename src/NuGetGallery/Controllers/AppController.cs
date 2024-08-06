﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Threading;
using System.Security.Claims;
using Microsoft.Owin;
using NuGetGallery.Cookies;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public abstract partial class AppController
        : Controller
    {
        private ICookieExpirationService _cookieExpirationService;
        private IFeatureFlagService _featureFlagService;

        private IOwinContext _overrideContext;

        public IOwinContext OwinContext => _overrideContext ?? HttpContext.GetOwinContext();

        public NuGetContext NuGetContext { get; }

        public new ClaimsPrincipal User
        {
            get { return base.User as ClaimsPrincipal; }
        }

        public void SetOwinContextOverride(IOwinContext owinContext)
        {
            _overrideContext = owinContext;
        }

        /// <summary>
        /// This method is used for the unit test.
        /// </summary>
        public void SetCookieExpirationService(ICookieExpirationService cookieExpirationService)
        {
            _cookieExpirationService = cookieExpirationService;
        }

        /// <summary>
        /// This method is used for the unit test.
        /// </summary>
        public void SetFeatureFlagsService(IFeatureFlagService featureFlagService)
        {
            _featureFlagService = featureFlagService;
        }

        protected AppController()
        {
            NuGetContext = new NuGetContext(this);

            _cookieExpirationService = GetService<ICookieExpirationService>();
            _featureFlagService = GetService<IFeatureFlagService>();
        }

        protected internal virtual T GetService<T>()
        {
            return DependencyResolver.Current.GetService<T>();
        }

        protected internal virtual User GetCurrentUser()
        {
            return OwinContext.GetCurrentUser();
        }

        protected internal virtual ActionResult SafeRedirect(string returnUrl)
        {
            return new SafeRedirectResult(returnUrl, Url.Home());
        }

        /// <summary>
        /// This method is to set TrySkipIisCustomErrors flag on failed requests so Json returns for failed requests don't get overwritten by IIS.
        /// </summary>
        /// <param name="statusCode">HTTP status code for response</param>
        /// <param name="obj">Object to Jsonify and return</param>
        /// <returns></returns>
        protected internal JsonResult Json(HttpStatusCode statusCode, object obj, JsonRequestBehavior jsonRequestBehavior)
        {
            Response.StatusCode = (int)statusCode;
            if (statusCode >= HttpStatusCode.BadRequest)
            {
                Response.TrySkipIisCustomErrors = true;
            }

            return Json(obj, jsonRequestBehavior);
        }

        protected internal JsonResult Json(HttpStatusCode statusCode)
        {
            return Json(statusCode, obj: new { }, jsonRequestBehavior: JsonRequestBehavior.DenyGet);
        }

        protected internal JsonResult Json(HttpStatusCode statusCode, object obj)
        {
            return Json(statusCode, obj, JsonRequestBehavior.DenyGet);
        }

        /// <summary>
        /// Called before the action method is invoked.
        /// </summary>
        /// <param name="filterContext">Information about the current request and action.</param>
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!filterContext.IsChildAction)
            {
                //no need to do the hassle for a child action
                //set the culture from the request headers
                var clientCulture = Request.DetermineClientCulture();
                if (clientCulture != null)
                {
                    Thread.CurrentThread.CurrentCulture = clientCulture;
                }
            }

            base.OnActionExecuting(filterContext);
        }

        /// <summary>
        /// Called after the action method is invoked.
        /// </summary>
        /// <param name="filterContext">Information about the current request and action.</param>
        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            SetCookieCompliance(filterContext);
            SetBannerFeatureFlag();

            base.OnActionExecuted(filterContext);
        }

        private void SetCookieCompliance(ActionExecutedContext filterContext)
        {
            if (filterContext.HttpContext?.Items[ServicesConstants.CookieComplianceCanWriteAnalyticsCookies] == null
                || (bool)filterContext.HttpContext.Items[ServicesConstants.CookieComplianceCanWriteAnalyticsCookies] == false)
            {
                ViewBag.CanWriteAnalyticsCookies = false;

                _cookieExpirationService.ExpireAnalyticsCookies(filterContext.HttpContext);
            }
            else
            {
                ViewBag.CanWriteAnalyticsCookies = true;
            }
        }

        private void SetBannerFeatureFlag()
        {
            if (_featureFlagService.IsDisplayBannerEnabled())
            {
                ViewBag.DisplayBanner = true;
            }
            else
            {
                ViewBag.DisplayBanner = false;
            }
        }
    }
}