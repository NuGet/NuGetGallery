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

        protected NuGetControllerBase(CommandExecutor executor)
        {
            Executor = executor;
        }
    }
}