// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace GitHubVulnerabilities2Db.GraphQL
{
    /// <summary>
    /// Interface for queryable types returned by the GraphQL API.
    /// </summary>
    public interface INode
    {
        DateTimeOffset UpdatedAt { get; set; }
    }
}
