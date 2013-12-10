using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Ninject;
using NuGetGallery.Configuration;

namespace NuGetGallery.Views
{
    public abstract class NuGetViewBase : WebViewPage
    {
        private Lazy<NuGetContext> _nugetContext;

        public NuGetContext NuGetContext
        {
            get { return _nugetContext.Value; }
        }

        public ConfigurationService Config
        {
            get { return NuGetContext.Config; }
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
        private Lazy<NuGetContext> _nugetContext;

        public NuGetContext NuGetContext
        {
            get { return _nugetContext.Value; }
        }

        public ConfigurationService Config
        {
            get { return NuGetContext.Config; }
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