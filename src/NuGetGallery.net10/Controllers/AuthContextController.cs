// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using NuGetGallery.Authentication;

namespace NuGetGallery;

[ApiController]
[AllowAnonymous]
[Route(Route)]
public sealed class AuthContextController : ControllerBase
{
    public const string Route = "/_local/auth-context";
    private readonly IWebHostEnvironment _environment;

    public AuthContextController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Get()
    {
        // Defense in depth if controller mapping changes in the future.
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        bool isAuthenticated = User.Identity?.IsAuthenticated == true;
        // Explicit allowlist: never serialize the principal, all claims, or ticket properties.
        return Ok(new
        {
            Host = "NuGetGallery.net10",
            IsAuthenticated = isAuthenticated,
            AuthenticationType = isAuthenticated ? User.Identity.AuthenticationType : null,
            Name = isAuthenticated ? User.Identity.Name : null,
            NameIdentifier = isAuthenticated
                ? User.FindFirst(SharedCookieConstants.NameIdentifierClaimType)?.Value
                : null,
            Roles = isAuthenticated
                ? User.FindAll(SharedCookieConstants.RoleClaimType)
                    .Select(claim => claim.Value)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(role => role, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>(),
        });
    }
}
