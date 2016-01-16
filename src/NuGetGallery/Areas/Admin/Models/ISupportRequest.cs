// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Data.Entity;
using NuGetGallery.Areas.Admin.Models;

namespace NuGetGallery
{
    public interface ISupportRequest 
    {
        DbSet<Admin> Admins { get; set; }
        DbSet<Issue> Issues { get; set; }
        DbSet <History> Histories { get; set; }
        DbSet<IssueStatus> IssueStatus { get; set; }

        void CommitChanges();
    }
}