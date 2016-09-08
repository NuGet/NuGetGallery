﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;
using NuGetGallery.Configuration;

namespace NuGetGallery
{
    public abstract partial class AppController
        : Controller
    {
        private IOwinContext _overrideContext;

        public IOwinContext OwinContext
        {
            get { return _overrideContext ?? HttpContext.GetOwinContext(); }
            set { _overrideContext = value; }
        }

        public NuGetContext NuGetContext { get; private set; }

        public new ClaimsPrincipal User
        {
            get { return base.User as ClaimsPrincipal; }
        }

        protected AppController()
        {
            NuGetContext = new NuGetContext(this);
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
        /// Called before the action method is invoked.
        /// </summary>
        /// <param name="filterContext">Information about the current request and action.</param>
        protected override async void OnActionExecuting(ActionExecutingContext filterContext)
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

            // This check is for the unit tests. Normally this should never be null.
            // (NuGetGallery.StatisticsControllerFacts+TheTotalsAllAction.UseClientCultureIfLanguageHeadersIsPresent)
            if (NuGetContext.Config != null)
            {
                ViewBag.CurrentConfig = await NuGetContext.Config.GetCurrent();
                ViewBag.CurrentFeatures = await NuGetContext.Config.GetFeatures();
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
