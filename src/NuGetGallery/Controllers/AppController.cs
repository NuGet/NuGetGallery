// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin;

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

        protected internal User GetCurrentUser()
        {
            return OwinContext.GetCurrentUser();
        }

        protected internal virtual ActionResult SafeRedirect(string returnUrl)
        {
            return new SafeRedirectResult(returnUrl, Url.Home());
        }
    }
}