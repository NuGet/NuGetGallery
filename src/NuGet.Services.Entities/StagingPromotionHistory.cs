// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NuGet.Services.Entities
{
    public class StagingPromotionHistory : IEntity
    {
        public StagingPromotionHistory()
        {
            Artifacts = new HashSet<StagingPromotionArtifactHistory>();
        }

        public int Key { get; set; }

        public Guid Id { get; set; }

        public int OwnerKey { get; set; }

        public User Owner { get; set; }

        public int? ApproverUserKey { get; set; }

        public User ApproverUser { get; set; }

        public StagingPromotionHistoryScope Scope { get; set; }

        public int? GroupKey { get; set; }

        public StagingGroup Group { get; set; }

        [StringLength(64)]
        public string GroupId { get; set; }

        [StringLength(256)]
        public string GroupName { get; set; }

        public DateTime RequestedDate { get; set; }

        public DateTime? CompletedDate { get; set; }

        public StagingPromotionHistoryStatus Status { get; set; }

        public DateTime? FailureNotificationQueuedDate { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual ICollection<StagingPromotionArtifactHistory> Artifacts { get; set; }
    }
}
