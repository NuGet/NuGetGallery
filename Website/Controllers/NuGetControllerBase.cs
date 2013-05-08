using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Commands;

namespace NuGetGallery
{
    public abstract class NuGetControllerBase : Controller
    {
        public CommandExecutor Executor { get; protected set; }

        // ONLY for use by T4MVC
        [Obsolete("Do not use! Only for use by T4MVC")]
        protected NuGetControllerBase()
        {
        }

        protected NuGetControllerBase(CommandExecutor executor)
        {
            Executor = executor;
        }
    }
}