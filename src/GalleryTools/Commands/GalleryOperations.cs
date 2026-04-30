// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
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

		public GalleryOperations (IEntitiesContext context, ICredentialBuilder credentialBuilder)
		{
			_context = context ?? throw new ArgumentNullException (nameof (context));
			_credentialBuilder = credentialBuilder ?? throw new ArgumentNullException (nameof (credentialBuilder));
		}

		/// <summary>
		/// Creates a user if one does not already exist. Saves changes immediately.
		/// </summary>
		public async Task<User> EnsureUserAsync (string username, string password, string email)
		{
			var existing = _context.Users.FirstOrDefault (u => u.Username == username);
			if (existing != null)
			{
				Console.WriteLine ($"User '{username}' already exists (key={existing.Key}).");
				return existing;
			}

			var passwordCredential = _credentialBuilder.CreatePasswordCredential (password);

			var user = new User (username)
			{
				EmailAllowed = true,
				EmailAddress = email,
				EmailConfirmationToken = null,
				NotifyPackagePushed = true,
				CreatedUtc = DateTime.UtcNow,
			};
			user.Credentials.Add (passwordCredential);

			_context.Users.Add (user);
			await _context.SaveChangesAsync ();

			Console.WriteLine ($"Created user '{username}' (key={user.Key}, email={email}).");
			return user;
		}

		/// <summary>
		/// Creates an organization if one does not already exist. Saves changes immediately.
		/// Returns null if the admin user is not found.
		/// </summary>
		public async Task<Organization> EnsureOrganizationAsync (string orgName, User admin, User collaborator)
		{
			var existing = _context.Users.FirstOrDefault (u => u.Username == orgName);
			if (existing != null)
			{
				Console.WriteLine ($"Organization '{orgName}' already exists (key={existing.Key}).");
				return existing as Organization;
			}

			var org = new Organization (orgName)
			{
				EmailAllowed = true,
				EmailAddress = $"{orgName}@localhost",
				EmailConfirmationToken = null,
				CreatedUtc = DateTime.UtcNow,
			};

			org.Members.Add (new Membership
			{
				Organization = org,
				Member = admin,
				IsAdmin = true,
			});

			if (collaborator != null)
			{
				org.Members.Add (new Membership
				{
					Organization = org,
					Member = collaborator,
					IsAdmin = false,
				});
			}

			_context.Users.Add (org);
			await _context.SaveChangesAsync ();

			Console.WriteLine ($"Created organization '{orgName}' (key={org.Key}, admin={admin.Username}).");
			return org;
		}

		/// <summary>
		/// Creates an API key credential and returns the plaintext key.
		/// Does NOT call SaveChangesAsync — caller should batch saves.
		/// </summary>
		public string CreateApiKey (User user, string description, string[] scopeActions, User scopeOwner)
		{
			var credential = _credentialBuilder.CreateApiKey (expiration: null, out string plaintextApiKey);
			credential.Description = description;
			credential.User = user;
			credential.UserKey = user.Key;
			credential.Scopes = _credentialBuilder.BuildScopes (scopeOwner, scopeActions, subjects: null);

			user.Credentials.Add (credential);

			Console.WriteLine ($"Created API key '{description}' for '{user.Username}' (scope owner={scopeOwner.Username}).");
			return plaintextApiKey;
		}
	}
}
