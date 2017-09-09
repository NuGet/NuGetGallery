using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.Mvc;
using Xunit;

namespace NuGetGallery.Controllers
{
    public class ControllerTests
    {
        private class AntiForgeryTokenException : IEquatable<AntiForgeryTokenException>
        {
            public Type Controller { get; private set; }

            public string Name { get; private set; }

            public AntiForgeryTokenException(Type controller, string name)
            {
                Controller = controller;
                Name = name;
            }

            public AntiForgeryTokenException(MethodInfo method)
            {
                Controller = method.DeclaringType;
                Name = method.Name;
            }
            
            public bool Equals(AntiForgeryTokenException other)
            {
                return other != null && other.Controller == Controller && other.Name == Name;
            }

            public override int GetHashCode()
            {
                return Controller.GetHashCode() ^ Name.GetHashCode();
            }
        }

        [Fact]
        public void AllActionsHaveAntiForgeryTokenIfNotGet()
        {
            // Arrange

            // If an action only supports these verbs, it doesn't need the anti-forgery token attribute.
            var verbExceptions = new HttpVerbs[]
            {
                HttpVerbs.Get,
                HttpVerbs.Head
            };

            // These actions cannot have the AntiForgery token attribute.
            // For example: API methods intended to be called by clients.
            var actionExceptions = new AntiForgeryTokenException[]
            {
                new AntiForgeryTokenException(typeof(ApiController), nameof(ApiController.CreatePackagePut)),
                new AntiForgeryTokenException(typeof(ApiController), nameof(ApiController.CreatePackagePost)),
                new AntiForgeryTokenException(typeof(ApiController), nameof(ApiController.CreatePackageVerificationKeyAsync)),
                new AntiForgeryTokenException(typeof(ApiController), nameof(ApiController.DeletePackage)),
                new AntiForgeryTokenException(typeof(ApiController), nameof(ApiController.PublishPackage)),
                new AntiForgeryTokenException(typeof(AuthenticationController), nameof(AuthenticationController.ChallengeAuthentication))
            };

            // Act
            var assembly = Assembly.GetAssembly(typeof(AppController));

            var actions = assembly.GetTypes()
                // Get all types that are controllers.
                .Where(t => typeof(Controller).IsAssignableFrom(t))
                // Get all public methods of those types.
                .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public))
                // Filter out compiler generated methods.
                .Where(m => !m.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Any())
                // Filter out methods that only support verbs that are exceptions.
                .Where(m =>
                {
                    var attributes = m.GetCustomAttributes()
                        .Where(a => typeof(ActionMethodSelectorAttribute).IsAssignableFrom(a.GetType()));

                    return !(attributes
                        .All(a =>
                            a.GetType() == typeof(HttpGetAttribute) ||
                            a.GetType() == typeof(HttpHeadAttribute) ||
                            (a.GetType() == typeof(AcceptVerbsAttribute) &&
                                (a as AcceptVerbsAttribute).Verbs.All(v =>
                                    verbExceptions.Any(ve => v.Equals(ve.ToString(), StringComparison.InvariantCultureIgnoreCase))))));
                })
                // Filter out exceptions.
                .Where(m =>
                 {
                     var possibleException = new AntiForgeryTokenException(m);
                     return !actionExceptions.Any(a => a.Equals(possibleException));
                 });

            // Assert
            var actionsMissingAntiForgeryToken = actions
                .Where(m => !(m.GetCustomAttributes()
                    .Any(a => a.GetType() == typeof(ValidateAntiForgeryTokenAttribute))));
            
            Assert.Empty(actionsMissingAntiForgeryToken);
        }
    }
}
