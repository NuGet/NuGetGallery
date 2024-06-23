// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public interface ICoreCertificateService
    {
        /// <summary>
        /// Add a certificate to the database if the certificate does not already exist.
        /// </summary>
        /// <param name="certificateStream">The certificate stream.</param>
        /// <returns>A task that represents the asynchronous operation.
        /// The task result (<see cref="Task{TResult}.Result" />) returns a <see cref="Certificate" /> 
        /// entity.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="certificateStream" /> is <c>null</c>.</exception>
        Task<Certificate> AddCertificateAsync(Stream certificateStream);
    }
}