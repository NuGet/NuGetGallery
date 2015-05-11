// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
namespace NuGetGallery.Migrations
{
    using System.Data.Entity.Migrations;
    
    public partial class AggregateStatistics_TotalDownloadCount : SqlResourceMigration
    {
        public AggregateStatistics_TotalDownloadCount()
            : base("NuGetGallery.Infrastructure.AggregateStatistics.sql")
        {
        }
    }
}
