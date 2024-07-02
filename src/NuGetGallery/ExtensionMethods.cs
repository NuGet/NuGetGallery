// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using NuGet.Frameworks;
using NuGet.Services.Entities;
using NuGetGallery.Helpers;

namespace NuGetGallery
{
    public static class ExtensionMethods
    {

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

        public static bool IsError<TModel, TProperty>(this HtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TProperty>> expression)
        {
            var metadata = ModelMetadata.FromLambdaExpression(expression, htmlHelper.ViewData);
            var name = htmlHelper.NameFor(expression).ToString();
            var modelState = htmlHelper.ViewData.ModelState[name];
            return modelState != null && modelState.Errors != null && modelState.Errors.Count > 0;
        }

        public static HtmlString HasErrorFor<TModel, TProperty>(this HtmlHelper<TModel> htmlHelper, Expression<Func<TModel, TProperty>> expression)
        {
            if (IsError(htmlHelper, expression))
            {
                return MvcHtmlString.Create("has-error-brand");
            }
            else
            {
                return MvcHtmlString.Empty;
            }
        }

        public static HtmlString ShowLabelFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            return ShowLabelFor(html, expression, labelText: null, isrequired: false);
        }

        public static HtmlString ShowLabelFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, bool isrequired)
        {
            return ShowLabelFor(html, expression, labelText: null, isrequired);
        }

        public static HtmlString ShowLabelFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, string labelText, bool isrequired)
        {
            var metadata = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            var propertyName = metadata.PropertyName.ToLower();

            if (isrequired)
            {
                return html.LabelFor(expression, labelText, new
                {
                    id = $"{propertyName}-label",
                    @class = "required"
                });
            }
            else
            {
                return html.LabelFor(expression, labelText, new
                {
                    id = $"{propertyName}-label"
                });
            }
        }

        public static HtmlString ShowPasswordFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            var htmlAttributes = GetHtmlAttributes(html, expression);
            htmlAttributes["autocomplete"] = "off";
            return html.PasswordFor(expression, htmlAttributes);
        }

        public static HtmlString ShowPasswordFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, string customClass)
        {
            var htmlAttributes = GetHtmlAttributes(html, expression, customClass: customClass);
            htmlAttributes["autocomplete"] = "off";
            return html.PasswordFor(expression, htmlAttributes);
        }

        public static HtmlString ShowTextBoxFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, bool enabled = true, string placeholder = null)
        {
            var htmlAttributes = GetHtmlAttributes(html, expression);
            if (!enabled)
            {
                htmlAttributes.Add("disabled", "true");
            }

            if (placeholder != null)
            {
                htmlAttributes.Add("placeholder", placeholder);
            }

            return html.TextBoxFor(expression, htmlAttributes);
        }

        public static HtmlString ShowTextBoxFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, string customClass, bool enabled = true, string placeholder = null)
        {
            var htmlAttributes = GetHtmlAttributes(html, expression, customClass: customClass);
            if (!enabled)
            {
                htmlAttributes.Add("disabled", "true");
            }

            if (placeholder != null)
            {
                htmlAttributes.Add("placeholder", placeholder);
            }

            return html.TextBoxFor(expression, htmlAttributes);
        }

        public static HtmlString ShowEmailBoxFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, string customClass = "")
        {
            var htmlAttributes = GetHtmlAttributes(html, expression, customClass: customClass);
            htmlAttributes["type"] = "email";
            return html.TextBoxFor(expression, htmlAttributes);
        }

        public static HtmlString ShowCheckboxFor<TModel>(this HtmlHelper<TModel> html, Expression<Func<TModel, bool>> expression, string customClass = "")
        {
            var htmlAttributes = GetHtmlAttributes(html, expression, isFormControl: false, isCheckbox: true, customClass);
            return html.CheckBoxFor(expression, htmlAttributes);
        }
        
        public static HtmlString ShowTextAreaFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, int rows, int columns)
        {
            var htmlAttributes = GetHtmlAttributes(html, expression);
            return html.TextAreaFor(expression, rows, columns, htmlAttributes);
        }

        public static HtmlString ShowTextAreaFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression, int rows, int columns, string customClass = "")
        {
            var htmlAttributes = GetHtmlAttributes(html, expression, customClass: customClass);
            return html.TextAreaFor(expression, rows, columns, htmlAttributes);
        }

        public static MvcHtmlString ShowEnumDropDownListFor<TModel, TEnum>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TEnum?>> expression,
            IEnumerable<TEnum> values,
            string emptyItemText)
          where TEnum : struct
        {
            var htmlAttributes = GetHtmlAttributes(html, expression);
            return html.EnumDropDownListFor(expression, values, emptyItemText, htmlAttributes);
        }

        public static MvcHtmlString ShowEnumDropDownListFor<TModel, TEnum>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TEnum?>> expression,
            string emptyItemText,
            string customClass = "")
          where TEnum : struct
        {
            var values = Enum
                .GetValues(typeof(TEnum))
                .Cast<TEnum>();

            return ShowEnumDropDownListFor<TModel, TEnum>(html, expression, values, emptyItemText, customClass);
        }

        public static MvcHtmlString ShowEnumDropDownListFor<TModel, TEnum>(
            this HtmlHelper<TModel> html,
            Expression<Func<TModel, TEnum?>> expression,
            IEnumerable<TEnum> values,
            string emptyItemText,
            string customClass = "")
          where TEnum : struct
        {
            var htmlAttributes = GetHtmlAttributes(html, expression, customClass: customClass);
            return html.EnumDropDownListFor(expression, values, emptyItemText, htmlAttributes);
        }

        private static Dictionary<string, object> GetHtmlAttributes<TModel, TProperty>(
            HtmlHelper<TModel> html,
            Expression<Func<TModel, TProperty>> expression,
            bool isFormControl = true,
            bool isCheckbox = false,
            string customClass = "")
        {
            var metadata = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            var propertyName = metadata.PropertyName.ToLower();
            var htmlAttributes = new Dictionary<string, object>();

            htmlAttributes["aria-labelledby"] = $"{propertyName}-label {propertyName}-validation-message";

            if (isFormControl)
            {
                htmlAttributes["class"] = $"form-control {customClass}";
            }
            else if (!string.IsNullOrEmpty(customClass))
            {
                htmlAttributes["class"] = $"{customClass}";
            }

            // If the property is required, mark it as required unless it's a checkbox.
            // Hearing that a checkbox is required is very confusing to screen readers.
            if (!isCheckbox && metadata.IsRequired)
            {
                htmlAttributes["aria-required"] = "true";
            }

            return htmlAttributes;
        }

        public static HtmlString ShowValidationMessagesFor<TModel, TProperty>(this HtmlHelper<TModel> html, Expression<Func<TModel, TProperty>> expression)
        {
            var metadata = ModelMetadata.FromLambdaExpression(expression, html.ViewData);
            var propertyName = metadata.PropertyName.ToLower();

            return html.ValidationMessageFor(expression, validationMessage: null, htmlAttributes: new Dictionary<string, object>(ValidationHtmlAttributes)
            {
                { "id", $"{propertyName}-validation-message" },
            });
        }

        public static MvcHtmlString ShowValidationMessagesForEmpty(this HtmlHelper html)
        {
            return html.ValidationMessage(modelName: string.Empty, htmlAttributes: ValidationHtmlAttributes);
        }

        private static IDictionary<string, object> ValidationHtmlAttributes = new Dictionary<string, object>
        {
            { "class", "help-block" },
            { "role", "alert" },
            { "aria-live", "assertive" },
        };

        public static string ToFriendlyName(this NuGetFramework frameworkName, bool allowRecurseProfile = true)
        {
            if (frameworkName == null)
            {
                throw new ArgumentNullException(nameof(frameworkName));
            }

            // Defer to the NuGet client logic for displaying .NET 5 frameworks. This aligns with Visual Studio package
            // management UI.
            var isNet5Era = frameworkName.Version.Major >= 5
                && StringComparer.OrdinalIgnoreCase.Equals(FrameworkConstants.FrameworkIdentifiers.NetCoreApp, frameworkName.Framework);
            if (isNet5Era)
            {
                return frameworkName.ToString();
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

                    sb.Append(')');
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

        /// <summary>
        /// This method will add the claim to the OwinContext with default value and update the cookie with the updated claims
        /// </summary>
        /// <returns>True if successfully adds the claim to the context, false otherwise</returns>
        public static bool AddClaim(this IOwinContext self, string claimType, string claimValue = null)
        {
            var identity = GetIdentity(self);
            if (identity == null || !identity.IsAuthenticated)
            {
                return false;
            }

            if (identity.TryAddClaim(claimType, claimValue))
            {
                // Update the cookies for the newly added claim
                self.Authentication.AuthenticationResponseGrant = new AuthenticationResponseGrant(new ClaimsPrincipal(identity), new AuthenticationProperties() { IsPersistent = true });
                return true;
            }

            return false;
        }

        /// <summary>
        /// This method will remove the claim from the OwinContext and update the cookie with the updated claims
        /// </summary>
        /// <returns>True if successfully removed the claim from context, false otherwise</returns>
        public static bool RemoveClaim(this IOwinContext self, string claimType)
        {
            var identity = GetIdentity(self);
            if (identity == null || !identity.IsAuthenticated)
            {
                return false;
            }

            if (identity.TryRemoveClaim(claimType))
            {
                // Update the cookies for the removed claim
                self.Authentication.AuthenticationResponseGrant = new AuthenticationResponseGrant(new ClaimsPrincipal(identity), new AuthenticationProperties() { IsPersistent = true });
                return true;
            }

            return false;
        }

        private static IIdentity GetIdentity(IOwinContext context)
        {
            var responseGrantIdentity = context.Authentication?.AuthenticationResponseGrant?.Identity;
            var authenticatedUserIdentity = context.Authentication?.User?.Identity;
            return responseGrantIdentity ?? authenticatedUserIdentity;
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
