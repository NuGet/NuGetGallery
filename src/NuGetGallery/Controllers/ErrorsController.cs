// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Web.Mvc;

namespace NuGetGallery
{
    public partial class ErrorsController : AppController
    {
        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Head)]
        public virtual ActionResult NotFound()
        {
            return View();
        }

        [HttpGet]
        public virtual ActionResult InternalError()
        {
            return View();
        }
    }
}