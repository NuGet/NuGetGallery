// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Configuration;
using NuGetGallery.Cookies;

namespace NuGetGallery.Views
{
    public abstract class NuGetViewBase : WebViewPage
    {
        private readonly Lazy<NuGetContext> _nugetContext;
        private readonly Lazy<CookieConsentMessage> _cookieConsentMessage;

        public NuGetContext NuGetContext
        {
            get { return _nugetContext.Value; }
        }

        public IGalleryConfigurationService Config
        {
            get { return NuGetContext.Config; }
        }

        public User CurrentUser
        {
            get { return NuGetContext.CurrentUser; }
        }

        public Lazy<IContentObjectService> ContentObjectService => new Lazy<IContentObjectService>(() => DependencyResolver.Current.GetService<IContentObjectService>());

        public CookieConsentMessage CookieConsentMessage
        {
            get { return _cookieConsentMessage.Value; }
        }

        public bool ShowAuthInHeader => true;

        protected NuGetViewBase()
        {
            _nugetContext = new Lazy<NuGetContext>(GetNuGetContextThunk(this));
            _cookieConsentMessage = new Lazy<CookieConsentMessage>(() => NuGetContext.GetCookieConsentMessage(Request));
        }

        internal static Func<NuGetContext> GetNuGetContextThunk(WebViewPage self)
        {
            return () =>
            {
                var ctrl = self.ViewContext.Controller as AppController;
                if (ctrl == null)
                {
                    throw new InvalidOperationException("NuGetViewBase should only be used on views for actions on AppControllers");
                }
                return ctrl.NuGetContext;
            };
        }

        protected override void InitializePage()
        {
            base.InitializePage();
            ViewBag.Sections = new Lazy<List<string>>(() => new List<string>());
        }
    }

    public abstract class NuGetViewBase<T> : WebViewPage<T>
    {
        private readonly Lazy<NuGetContext> _nugetContext;
        private readonly Lazy<CookieConsentMessage> _cookieConsentMessage;

        public NuGetContext NuGetContext
        {
            get { return _nugetContext.Value; }
        }

        public IGalleryConfigurationService Config
        {
            get { return NuGetContext.Config; }
        }

        public User CurrentUser
        {
            get { return NuGetContext.CurrentUser; }
        }

        public Lazy<IContentObjectService> ContentObjectService => new Lazy<IContentObjectService>(() => DependencyResolver.Current.GetService<IContentObjectService>());

        public CookieConsentMessage CookieConsentMessage
        {
            get { return _cookieConsentMessage.Value; }
        }

        public IFeatureFlagService Features => NuGetContext.Features;

        public bool ShowAuthInHeader => true;

        public bool LinkOpenSearchXml => true;

        protected NuGetViewBase()
        {
            _nugetContext = new Lazy<NuGetContext>(NuGetViewBase.GetNuGetContextThunk(this));
            _cookieConsentMessage = new Lazy<CookieConsentMessage>(() => NuGetContext.GetCookieConsentMessage(Request));
        }

        protected override void InitializePage()
        {
            base.InitializePage();
            ViewBag.Sections = new Lazy<List<string>>(() => new List<string>());
        }
    }
}