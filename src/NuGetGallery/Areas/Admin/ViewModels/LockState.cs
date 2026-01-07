// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGetGallery.Areas.Admin.ViewModels
{
    public class LockState
    {
        /// <summary>
        /// This can be an identifying string for a lockable entity, e.g. package's ID or user's username.
        /// </summary>
        public string Identifier { get; set; }
        public bool IsLocked { get; set; }
    }
}