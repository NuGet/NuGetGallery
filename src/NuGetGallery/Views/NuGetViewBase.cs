﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web.Mvc;
using NuGetGallery.Configuration;
using System.Threading.Tasks;

namespace NuGetGallery.Views
{
    public abstract class NuGetViewBase : WebViewPage
    {
        private readonly Lazy<NuGetContext> _nugetContext;

        public NuGetContext NuGetContext
        {
            get { return _nugetContext.Value; }
        }

        public User CurrentUser
        {
            get { return NuGetContext.CurrentUser; }
        }

        protected NuGetViewBase()
        {
            _nugetContext = new Lazy<NuGetContext>(GetNuGetContextThunk(this));
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
    }

    public abstract class NuGetViewBase<T> : WebViewPage<T>
    {
        private readonly Lazy<NuGetContext> _nugetContext;

        public NuGetContext NuGetContext
        {
            get { return _nugetContext.Value; }
        }

        public User CurrentUser
        {
            get { return NuGetContext.CurrentUser; }
        }

        protected NuGetViewBase()
        {
            _nugetContext = new Lazy<NuGetContext>(NuGetViewBase.GetNuGetContextThunk(this));
        }
    }
}