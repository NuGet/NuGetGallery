// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.Threading.Tasks;
using System.Web.Mvc;

namespace NuGetGallery
{
    public interface IStatusService
    {
        Task<ActionResult> GetStatus();
    }
}