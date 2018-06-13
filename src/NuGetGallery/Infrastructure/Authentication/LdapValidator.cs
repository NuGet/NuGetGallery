// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery.Authentication.Providers.LdapUser;
using NuGetGallery.Configuration;
using System;
using System.DirectoryServices;
using System.Linq;
using System.Web.Mvc;

namespace NuGetGallery.Infrastructure.Authentication
{
    public static class LdapValidator
    {
        static LdapValidator()
        {
            var configuration = DependencyResolver.Current.GetService<ConfigurationService>();
            BaseConfig = configuration.ResolveConfigObject(new LdapUserAuthenticatorConfiguration(), "Auth.LdapUser.").Result;
        }

        public static LdapUserAuthenticatorConfiguration BaseConfig { get; }

        /// <summary>
        /// Returns a <see cref="bool"/> indicating the result of a hash comparison.
        /// </summary>
        /// <param name="username">The user name.</param>
        /// <param name="password">The hashed password supplied for comparison.</param>
        /// <returns>A <see cref="bool"/> indicating the result of a hash comparison.</returns>
        public static bool ValidateUser(string username, string password)
        {
            try
            {
                var servicePath = "LDAP://" + BaseConfig.Host + ":" + BaseConfig.Port + "/" + BaseConfig.UserBase;
                DirectoryEntry ldapConnection;
                if (string.IsNullOrEmpty(BaseConfig.ServiceAccountUserName) || string.IsNullOrEmpty(BaseConfig.ServiceAccountPassword))
                    ldapConnection = new DirectoryEntry(servicePath);
                else
                    ldapConnection = new DirectoryEntry(servicePath, BaseConfig.ServiceAccountUserName, BaseConfig.ServiceAccountPassword);

                var search = new DirectorySearcher(ldapConnection)
                {
                    Filter = $"(&{BaseConfig.ObjectFilter}({BaseConfig.NameAttribute}={username}))"
                };
                search.PropertiesToLoad.AddRange(new[] { BaseConfig.NameAttribute, BaseConfig.GroupAttribute });

                // This CAN return null if nothing was found:
                var result = search.FindOne();
                if (result == null)
                    return false;

                var usernameLdap = result.Properties[BaseConfig.NameAttribute][0].ToString();
                // Check username:
                if (string.Compare(usernameLdap, username, StringComparison.CurrentCultureIgnoreCase) != 0)
                    return false;

                // Check password:
                var validatedUser = ValidateDirectoryUser(result.Path, usernameLdap, password);
                if (validatedUser == null)
                    return false;

                // Check if is in allowed group:
                if (validatedUser.Properties[BaseConfig.GroupAttribute].OfType<string>().All(group => string.Compare(group, BaseConfig.AllowedGroup, StringComparison.CurrentCultureIgnoreCase) != 0))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DirectoryEntry ValidateDirectoryUser(string path, string userName, string userPassword)
        {
            try
            {
                var validatedUser = new DirectoryEntry(path, userName, userPassword);
                if (validatedUser.NativeObject != null)
                    return validatedUser;
            }
            catch (DirectoryServicesCOMException)
            {
            }
            return null;
        }
    }
}