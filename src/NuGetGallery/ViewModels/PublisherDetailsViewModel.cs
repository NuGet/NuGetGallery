// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace NuGetGallery
{
    [DebuggerDisplay("{Name,nq}")]
    public abstract class PublisherDetailsViewModel
    {
        protected PublisherDetailsViewModel() { }

        /// <summary>
        /// Publisher name, e.g. GitHub. Writable to be JSON serializable.
        /// </summary>
        [Required]
        public abstract string Name { get; }

        /// <summary>
        /// Validates data in the view model. Returns error message if validation fails, otherwise returns null or empty string.
        /// </summary>
        /// <returns></returns>
        public abstract string Validate();

        /// <summary>
        /// Creates a copy of itself and updates it with JSON data.
        /// </summary>
        /// <param name="javaScriptJson">A JSON string containing the updated details. Propery names match existing C# property names.</param>
        public abstract PublisherDetailsViewModel Update(string javaScriptJson);

        public abstract string ToDatabaseJson();
    }
}
