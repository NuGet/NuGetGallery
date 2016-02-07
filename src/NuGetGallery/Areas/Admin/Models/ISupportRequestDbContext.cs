// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.Entity;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery
{
    public interface ISupportRequestDbContext
    {
        IDbSet<Admin> Admins { get; set; }
        IDbSet<Issue> Issues { get; set; }
        IDbSet <History> Histories { get; set; }
        IDbSet<IssueStatus> IssueStatus { get; set; }

        void CommitChanges();
    }
}