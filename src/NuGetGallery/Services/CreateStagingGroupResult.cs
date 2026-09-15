// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    /// <summary>
    /// Describes the outcome of creating a staging group.
    /// </summary>
    public class CreateStagingGroupResult
    {
        private CreateStagingGroupResult(CreateStagingGroupResultType type, StagingGroup group)
        {
            Type = type;
            Group = group;
        }

        /// <summary>
        /// Gets the creation outcome.
        /// </summary>
        public CreateStagingGroupResultType Type { get; }

        /// <summary>
        /// Gets the created group when <see cref="Type"/> is <see cref="CreateStagingGroupResultType.Created"/>.
        /// </summary>
        public StagingGroup Group { get; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        /// <param name="group">The created staging group.</param>
        /// <returns>The successful result.</returns>
        public static CreateStagingGroupResult Created(StagingGroup group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            return new CreateStagingGroupResult(CreateStagingGroupResultType.Created, group);
        }

        /// <summary>
        /// Creates a result indicating that the group ID already exists for the owner.
        /// </summary>
        /// <returns>The duplicate-ID result.</returns>
        public static CreateStagingGroupResult GroupAlreadyExists()
        {
            return new CreateStagingGroupResult(CreateStagingGroupResultType.GroupAlreadyExists, group: null);
        }
    }

    /// <summary>
    /// Identifies the outcome of creating a staging group.
    /// </summary>
    public enum CreateStagingGroupResultType
    {
        /// <summary>
        /// The staging group was created.
        /// </summary>
        Created,

        /// <summary>
        /// The group ID already exists for the owner.
        /// </summary>
        GroupAlreadyExists,
    }
}
