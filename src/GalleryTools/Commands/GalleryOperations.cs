// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using NuGet.Services.Entities;
using NuGetGallery;
using NuGetGallery.Authentication;
using NuGetGallery.Infrastructure.Authentication;

namespace GalleryTools.Commands
{
    /// <summary>
    /// Shared database operations for GalleryTools commands.
    /// Used by both standalone commands (createuser, createorganization, createapikey)
    /// and the composite seedfunctionaltests command.
    /// </summary>
    public class GalleryOperations
    {
        private readonly IEntitiesContext _context;
        private readonly ICredentialBuilder _credentialBuilder;

        public GalleryOperations(IEntitiesContext context, ICredentialBuilder credentialBuilder)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _credentialBuilder = credentialBuilder ?? throw new ArgumentNullException(nameof(credentialBuilder));
        }

        /// <summary>
        /// Creates a user if one does not already exist. Saves changes immediately.
        /// </summary>
        public async Task<User> EnsureUserAsync(string username, string password, string email)
        {
            var existing = _context.Users.FirstOrDefault(u => u.Username == username);
            if (existing != null)
            {
                Console.WriteLine($"User '{username}' already exists (key={existing.Key}).");
                return existing;
            }

            var passwordCredential = _credentialBuilder.CreatePasswordCredential(password);

            var user = new User(username)
            {
                EmailAllowed = true,
                EmailAddress = email,
                EmailConfirmationToken = null,
                NotifyPackagePushed = true,
                CreatedUtc = DateTime.UtcNow,
            };
            user.Credentials.Add(passwordCredential);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Created user '{username}' (key={user.Key}, email={email}).");
            return user;
        }

        /// <summary>
        /// Creates an organization if one does not already exist. Saves changes immediately.
        /// Returns null if the admin user is not found.
        /// </summary>
        public async Task<Organization> EnsureOrganizationAsync(string orgName, User admin, User collaborator)
        {
            var existing = _context.Users.FirstOrDefault(u => u.Username == orgName);
            if (existing != null)
            {
                Console.WriteLine($"Organization '{orgName}' already exists (key={existing.Key}).");
                return existing as Organization;
            }

            var org = new Organization(orgName)
            {
                EmailAllowed = true,
                EmailAddress = $"{orgName}@localhost",
                EmailConfirmationToken = null,
                CreatedUtc = DateTime.UtcNow,
            };

            org.Members.Add(new Membership
            {
                Organization = org,
                Member = admin,
                IsAdmin = true,
            });

            if (collaborator != null)
            {
                org.Members.Add(new Membership
                {
                    Organization = org,
                    Member = collaborator,
                    IsAdmin = false,
                });
            }

            _context.Users.Add(org);
            await _context.SaveChangesAsync();

            Console.WriteLine($"Created organization '{orgName}' (key={org.Key}, admin={admin.Username}).");
            return org;
        }

        /// <summary>
        /// Creates an API key credential and returns the plaintext key.
        /// Does NOT call SaveChangesAsync — caller should batch saves.
        /// </summary>
        public string CreateApiKey(User user, string description, string[] scopeActions, User scopeOwner)
        {
            var credential = _credentialBuilder.CreateApiKey(expiration: null, out string plaintextApiKey);
            credential.Description = description;
            credential.User = user;
            credential.UserKey = user.Key;
            credential.Scopes = _credentialBuilder.BuildScopes(scopeOwner, scopeActions, subjects: null);

            user.Credentials.Add(credential);

            Console.WriteLine($"Created API key '{description}' for '{user.Username}' (scope owner={scopeOwner.Username}).");
            return plaintextApiKey;
        }

        /// <summary>
        /// Pushes a .nupkg file to the Gallery via the HTTP API.
        /// Handles 409 Conflict (already exists) and 403 Forbidden (owned by another account) gracefully.
        /// </summary>
        public static async Task PushPackageAsync(string baseUrl, string apiKey, string nupkgPath)
        {
            var fileName = Path.GetFileName(nupkgPath);

            // Trust dev certs for localhost HTTPS
            var handler = new HttpClientHandler();
            if (baseUrl.IndexOf("localhost", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true;
            }

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("X-NuGet-ApiKey", apiKey);
                client.DefaultRequestHeaders.Add("X-NuGet-Client-Version", "6.0.0");
                client.Timeout = TimeSpan.FromSeconds(120);

                using (var fileStream = File.OpenRead(nupkgPath))
                {
                    var content = new MultipartFormDataContent();
                    var streamContent = new StreamContent(fileStream);
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Add(streamContent, "package", fileName);

                    var pushUrl = $"{baseUrl.TrimEnd('/')}/api/v2/package";
                    var response = await client.PutAsync(pushUrl, content);

                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                        Console.WriteLine($"Package {fileName} already exists (409 Conflict). Skipping.");
                        return;
                    }

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        // Package may already exist under a different owner (e.g. seeded by AppHost).
                        Console.WriteLine($"Package {fileName} returned 403 Forbidden (likely already owned by another account). Skipping.");
                        return;
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync();
                        throw new InvalidOperationException(
                            $"Failed to push {fileName}: {response.StatusCode} {response.ReasonPhrase}\n{body}");
                    }

                    Console.WriteLine($"Pushed {fileName} via API ({response.StatusCode}).");
                }
            }
        }
    }
}
