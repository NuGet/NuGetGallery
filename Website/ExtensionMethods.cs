using System;
using System.Collections.Generic;
using System.Data.Services;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Security.Principal;
using System.ServiceModel.Activation;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.WebPages;

namespace NuGetGallery
{
    public static class ExtensionMethods
    {
        public static void MapServiceRoute(
            this RouteCollection routes,
            string routeName,
            string routeUrl,
            Type serviceType)
        {
            var serviceRoute = new ServiceRoute(routeUrl, new DataServiceHostFactory(), serviceType);
            serviceRoute.Defaults = new RouteValueDictionary { { "serviceType", "odata" } };
            serviceRoute.Constraints = new RouteValueDictionary { { "serviceType", "odata" }, { "httpMethod", new HttpMethodConstraint("GET") } };
            routes.Add(routeName, serviceRoute);
        }

        public static string ToStringSafe(this object obj)
        {
            if (obj != null)
            {
                return obj.ToString();
            }
            return String.Empty;
        }

        public static string Flatten(this IEnumerable<string> list)
        {
            if (list == null)
                return String.Empty;

            return String.Join(", ", list.ToArray());
        }
        
        public static string Flatten(this ICollection<PackageAuthor> authors)
        {
            return authors.Select(a => a.Name).Flatten();
        }

        public static string Flatten(this ICollection<PackageDependency> dependencies)
        {
            return FlattenDependencies(dependencies.Select(d => new Tuple<string, string>(d.Id, d.VersionSpec.ToStringSafe())));
        }

        public static string Flatten(this IEnumerable<NuGet.PackageDependency> dependencies)
        {
            return FlattenDependencies(dependencies.Select(d => new Tuple<string, string>(d.Id, d.VersionSpec.ToStringSafe())));
        }

        static string FlattenDependencies(IEnumerable<Tuple<string, string>> dependencies)
        {
            return String.Join("|", dependencies.Select(d => String.Format(CultureInfo.InvariantCulture, "{0}:{1}", d.Item1, d.Item2)).ToArray());
        }

        public static HelperResult Flatten<T>(this IEnumerable<T> items, Func<T, HelperResult> template)
        {
            if (items == null)
            {
                return null;
            }
            var formattedItems = items.Select(item => template(item).ToHtmlString());

            return new HelperResult(writer =>
            {
                writer.Write(String.Join(", ", formattedItems.ToArray()));
            });
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
                throw new ArgumentNullException("package");
            }
            if (user == null || user.Identity == null)
            {
                return false;
            }
            return user.IsInRole(Constants.AdminRoleName) || package.Owners.Any(u => u.Username == user.Identity.Name);
        }

        public static bool IsOwner(this PackageRegistration package, User user)
        {
            if (package == null)
            {
                throw new ArgumentNullException("package");
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

        public static bool IsInThePast(this DateTime? date)
        {
            return date.Value.IsInThePast();
        }

        public static bool IsInThePast(this DateTime date)
        {
            return date < DateTime.UtcNow;
        }

        public static IQueryable<T> SortBy<T>(this IQueryable<T> source, string sortExpression)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
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

            Expression methodCallExpression = Expression.Call(typeof(Queryable),
                                                              methodName,
                                                              new Type[] { 
                                                                  source.ElementType, 
                                                                  property.Type 
                                                              },
                                                              source.Expression,
                                                              Expression.Quote(lambda));

            return source.Provider.CreateQuery<T>(methodCallExpression);
        }

        public static MailAddress ToMailAddress(this User user)
        {
            return new MailAddress(user.EmailAddress, user.Username);
        }

        public static bool IsError<TModel, TProperty>(this HtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TProperty>> expression)
        {
            var metadata = ModelMetadata.FromLambdaExpression<TModel, TProperty>(expression, htmlHelper.ViewData);
            var modelState = htmlHelper.ViewData.ModelState[metadata.PropertyName];
            return modelState != null && modelState.Errors != null && modelState.Errors.Count > 0;
        }
    }
}