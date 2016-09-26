// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Security;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.WebPages;
using Microsoft.Owin;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGetGallery.Authentication;

namespace NuGetGallery
{
    public static class ExtensionMethods
    {
        public static void AddOrSet<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> self, TKey key, TValue val)
        {
            self.AddOrUpdate(key, val, (_, __) => val);
        }

        public static SecureString ToSecureString(this string str)
        {
            SecureString output = new SecureString();
            foreach (char c in str)
            {
                output.AppendChar(c);
            }
            output.MakeReadOnly();
            return output;
        }

        public static string ToStringOrNull(this object obj)
        {
            if (obj == null)
            {
                return null;
            }

            return obj.ToString();
        }

        public static string ToEncodedUrlStringOrNull(this Uri uri)
        {
            if (uri == null)
            {
                return null;
            }

            return uri.AbsoluteUri;
        }

        public static string ToStringSafe(this object obj)
        {
            if (obj != null)
            {
                return obj.ToString();
            }
            return String.Empty;
        }

        public static IEnumerable<PackageDependency> AsPackageDependencyEnumerable(this IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            foreach (var dependencyGroup in dependencyGroups)
            {
                if (!dependencyGroup.Packages.Any())
                {
                    yield return new PackageDependency
                    {
                        Id = null,
                        VersionSpec = null,
                        TargetFramework = dependencyGroup.TargetFramework.ToShortNameOrNull()
                    };
                }
                else
                {
                    foreach (var dependency in dependencyGroup.Packages.Select(
                        d => new { d.Id, d.VersionRange, dependencyGroup.TargetFramework }))
                    {
                        yield return new PackageDependency
                        {
                            Id = dependency.Id,
                            VersionSpec = dependency.VersionRange?.ToString(),
                            TargetFramework = dependency.TargetFramework.ToShortNameOrNull()
                        };
                    }
                }
            }
        }

        public static IEnumerable<PackageType> AsPackageTypeEnumerable(this IEnumerable<NuGet.Packaging.Core.PackageType> packageTypes)
        {
            foreach (var packageType in packageTypes)
            {
                yield return new PackageType
                {
                    Name = packageType.Name,
                    Version = packageType.Version.ToString()
                };
            }

        }

        public static string Flatten(this IEnumerable<string> list)
        {
            if (list == null)
            {
                return String.Empty;
            }

            return String.Join(", ", list.ToArray());
        }

        public static string Flatten(this IEnumerable<PackageDependencyGroup> dependencyGroups)
        {
            return FlattenDependencies(
                AsPackageDependencyEnumerable(dependencyGroups).ToList());
        }

        public static string Flatten(this IEnumerable<PackageType> packageTypes)
        {
            return String.Join("|", packageTypes.Select(d => String.Format(CultureInfo.InvariantCulture, "{0}:{1}", d.Name, d.Version)));
        }

        public static string Flatten(this ICollection<PackageDependency> dependencies)
        {
            return
                FlattenDependencies(
                    dependencies.Select(
                        d => new { d.Id, VersionSpec = d.VersionSpec.ToStringSafe(), TargetFramework = d.TargetFramework.ToStringSafe() }));
        }

        private static string FlattenDependencies(IEnumerable<dynamic> dependencies)
        {
            return String.Join(
                "|", dependencies.Select(d => String.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}", d.Id, d.VersionSpec, d.TargetFramework)));
        }

        public static HelperResult Flatten<T>(this IEnumerable<T> items, Func<T, HelperResult> template)
        {
            if (items == null)
            {
                return null;
            }
            var formattedItems = items.Select(item => template(item).ToHtmlString());

            return new HelperResult(writer => { writer.Write(String.Join(", ", formattedItems.ToArray())); });
        }

        public static bool AnySafe<T>(this IEnumerable<T> items)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any();
        }

        public static bool AnySafe<T>(this IEnumerable<T> items, Func<T, bool> predicate)
        {
            if (items == null)
            {
                return false;
            }
            return items.Any(predicate);
        }

        public static bool IsOwner(this Package package, IPrincipal user)
        {
            return package.PackageRegistration.IsOwner(user);
        }

        public static bool IsOwner(this Package package, User user)
        {
            return package.PackageRegistration.IsOwner(user);
        }

        public static bool IsOwner(this PackageRegistration package, IPrincipal user)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            if (user == null || user.Identity == null)
            {
                return false;
            }
            return user.IsAdministrator() || package.Owners.Any(u => u.Username == user.Identity.Name);
        }

        public static bool IsOwner(this PackageRegistration package, User user)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }
            if (user == null)
            {
                return false;
            }
            return package.Owners.Any(u => u.Key == user.Key);
        }

        // apple polish!
        public static string CardinalityLabel(this int count, string singular, string plural)
        {
            return count == 1 ? singular : plural;
        }

        public static IQueryable<T> SortBy<T>(this IQueryable<T> source, string sortExpression)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            int descIndex = sortExpression.IndexOf(" desc", StringComparison.OrdinalIgnoreCase);
            if (descIndex != -1)
            {
                sortExpression = sortExpression.Substring(0, descIndex).Trim();
            }

            if (String.IsNullOrEmpty(sortExpression))
            {
                return source;
            }

            ParameterExpression parameter = Expression.Parameter(source.ElementType);
            Expression property = sortExpression.Split('.')
                .Aggregate<string, Expression>(parameter, Expression.Property);

            LambdaExpression lambda = Expression.Lambda(property, parameter);

            string methodName = descIndex == -1 ? "OrderBy" : "OrderByDescending";

            Expression methodCallExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                new[]
                    {
                        source.ElementType,
                        property.Type
                    },
                source.Expression,
                Expression.Quote(lambda));

            return source.Provider.CreateQuery<T>(methodCallExpression);
        }

        public static MailAddress ToMailAddress(this User user)
        {
            if (!user.Confirmed)
            {
                return new MailAddress(user.UnconfirmedEmailAddress, user.Username);
            }

            return new MailAddress(user.EmailAddress, user.Username);
        }

        public static bool IsError<TModel, TProperty>(this HtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TProperty>> expression)
        {
            var metadata = ModelMetadata.FromLambdaExpression(expression, htmlHelper.ViewData);
            var modelState = htmlHelper.ViewData.ModelState[metadata.PropertyName];
            return modelState != null && modelState.Errors != null && modelState.Errors.Count > 0;
        }

        public static string ToShortNameOrNull(this NuGetFramework frameworkName)
        {
            if (frameworkName == null)
            {
                return null;
            }

            var shortFolderName = frameworkName.GetShortFolderName();

            // If the shortFolderName is "any", we want to return null to preserve NuGet.Core
            // compatibility in the V2 feed.
            if (String.Equals(shortFolderName, "any", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return shortFolderName;
        }

        public static string ToFriendlyName(this NuGetFramework frameworkName, bool allowRecurseProfile = true)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException(nameof(frameworkName));
            }

            var sb = new StringBuilder();
            if (String.Equals(frameworkName.Framework, ".NETPortable", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append("Portable Class Library");

                // Recursively parse the profile
                if (allowRecurseProfile)
                {
                    sb.Append(" (");

                    var profiles = frameworkName.GetShortFolderName()
                        .Replace("portable-", string.Empty)
                        .Replace("portable40-", string.Empty)
                        .Replace("portable45-", string.Empty)
                        .Split('+');

                    sb.Append(String.Join(", ",
                        profiles.Select(s => NuGetFramework.Parse(s).ToFriendlyName(allowRecurseProfile: false))));

                    sb.Append(")");
                }
            }
            else
            {
                string version = null;
                if (frameworkName.Version.Build == 0)
                {
                    version = frameworkName.Version.ToString(2);
                }
                else if (frameworkName.Version.Revision == 0)
                {
                    version = frameworkName.Version.ToString(3);
                }
                else
                {
                    version = frameworkName.Version.ToString();
                }

                sb.AppendFormat("{0} {1}", frameworkName.Framework, version);
                if (!String.IsNullOrEmpty(frameworkName.Profile))
                {
                    sb.AppendFormat(" {0}", frameworkName.Profile);
                }
            }
            return sb.ToString();
        }

        public static string GetClaimOrDefault(this ClaimsPrincipal self, string claimType)
        {
            return self.Claims.GetClaimOrDefault(claimType);
        }

        public static string GetClaimOrDefault(this ClaimsIdentity self, string claimType)
        {
            return self.Claims.GetClaimOrDefault(claimType);
        }

        public static string GetClaimOrDefault(this IEnumerable<Claim> self, string claimType)
        {
            return self
                .Where(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Value)
                .FirstOrDefault();
        }

        public static bool HasScope(this ClaimsIdentity self, params string[] scopes)
        {
            var scopeClaim = self.GetClaimOrDefault(NuGetClaims.Scope);

            if (string.IsNullOrEmpty(scopeClaim))
            {
                // Legacy API key, allow access...
                return true;
            }

            return scopeClaim.Split(';').AnySafe(scope => scopes.Any(s =>
                string.Equals(s, scope, StringComparison.OrdinalIgnoreCase)));
        }

        // This is a method because the first call will perform a database call
        /// <summary>
        /// Get the current user, from the database, or if someone in this request has already
        /// retrieved it, from memory. This will NEVER return null. It will throw an exception
        /// that will yield an HTTP 401 if it would return null. As a result, it should only
        /// be called in actions with the Authorize attribute or a Request.IsAuthenticated check
        /// </summary>
        /// <returns>The current user</returns>
        public static User GetCurrentUser(this IOwinContext self)
        {
            if (self.Request.User == null)
            {
                return null;
            }

            User user = null;
            object obj;
            if (self.Environment.TryGetValue(Constants.CurrentUserOwinEnvironmentKey, out obj))
            {
                user = obj as User;
            }

            if (user == null)
            {
                user = LoadUser(self);
                self.Environment[Constants.CurrentUserOwinEnvironmentKey] = user;
            }

            if (user == null)
            {
                // Unauthorized! If we get here it's because a valid session token was presented, but the
                // user doesn't exist any more. So we just have a generic error.
                throw new HttpException(401, Strings.Unauthorized);
            }

            return user;
        }

        private static User LoadUser(IOwinContext context)
        {
            var principal = context.Authentication.User;
            if (principal != null)
            {
                // Try to authenticate with the user name
                string userName = principal.GetClaimOrDefault(ClaimTypes.Name);

                if (!String.IsNullOrEmpty(userName))
                {
                    return DependencyResolver.Current
                        .GetService<IUserService>()
                        .FindByUsername(userName);
                }
            }
            return null; // No user logged in, or credentials could not be resolved
        }
    }
}
