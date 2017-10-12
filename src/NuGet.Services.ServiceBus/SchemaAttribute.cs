// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Services.ServiceBus
{
    /// <summary>
    /// The attribute used to define a schema.
    /// </summary>
    public class SchemaAttribute : Attribute
    {
        /// <summary>
        /// The name of a message's schema. This should NEVER change for a single message.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The schema's version. This should be bumped whenever a schema's property
        /// is added, removed, or modified.
        /// </summary>
        public int Version { get; set; }
    }
}
