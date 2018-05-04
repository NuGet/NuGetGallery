// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGetGallery.Auditing
{
    public enum AuditedPackageAction
    {
        Deleted, //deprecated
        Delete,
        SoftDelete,
        SoftDeleted, //deprecated
        Create,
        List,
        Unlist,
        Edit,
        [Obsolete("Undo package edit functionality is being retired.")]
        UndoEdit,
        Verify
    }
}