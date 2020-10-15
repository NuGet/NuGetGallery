// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGetGallery
{
    public enum DeletePackageAction
    {
        Unlist,
        SoftDelete,
    }

    public class DeletePackageApiRequest
    {
        /// <summary>
        /// We have a clunky string to enum mapping here because the default behavior of ASP.NET MVC model binding
        /// is not configurable enough for our purposes. If the model property type is an enum, an invalid incoming
        /// request value is set to be the default enum value, rather than rejecting this request. This behavior is too
        /// permissive.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, DeletePackageAction> Actions = Enum
            .GetValues(typeof(DeletePackageAction))
            .Cast<DeletePackageAction>()
            .ToDictionary(x => x.ToString(), x => x, StringComparer.OrdinalIgnoreCase);

        public string Type { get; set; }
    }
}
