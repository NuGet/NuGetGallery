// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.Status.Table
{
    /// <summary>
    /// Base implementation of <see cref="IAggregatedComponentAffectingEntity{T}"/>.
    /// </summary>
    public class AggregatedComponentAffectingEntity<TAggregation> : ComponentAffectingEntity, IAggregatedComponentAffectingEntity<TAggregation>
        where TAggregation : ComponentAffectingEntity
    {
        public AggregatedComponentAffectingEntity()
        {
            _parent = new ChildEntity<TAggregation>();
        }

        public AggregatedComponentAffectingEntity(
            string partitionKey, 
            string rowKey,
            TAggregation entity,
            string affectedComponentPath,
            DateTime startTime,
            ComponentStatus affectedComponentStatus = ComponentStatus.Up,
            DateTime? endTime = null)
            : base(
                  partitionKey, 
                  rowKey, 
                  affectedComponentPath, 
                  startTime, 
                  affectedComponentStatus, 
                  endTime)
        {
            _parent = new ChildEntity<TAggregation>(partitionKey, rowKey, entity);
        }

        private readonly ChildEntity<TAggregation> _parent;
        
        public string ParentRowKey
        {
            get { return _parent.ParentRowKey; }
            set { _parent.ParentRowKey = value; }
        }
        
        public bool IsLinked
        {
            get { return _parent.IsLinked; }
            set { _parent.IsLinked = value; }
        }
    }
}
