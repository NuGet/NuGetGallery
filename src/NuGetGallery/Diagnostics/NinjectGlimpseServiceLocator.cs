using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using Glimpse.Core.Configuration;
using Glimpse.Core.Framework;
using Ninject;

namespace NuGetGallery.Diagnostics
{
    public class NinjectGlimpseServiceLocator : IServiceLocator
    {
        public ICollection<T> GetAllInstances<T>() where T : class
        {
            var ninjectResources = Container.Kernel.GetAll<T>().ToList();

            // Glimpse interprets an empty collection to mean: I'm overriding your defaults and telling you NOT to load anythig
            // However, we want an empty collection to indicate to Glimpse that it should use the default. Returning null does that.
            if (ninjectResources.Any())
            {
                return ninjectResources;
            }

            var ignoredTypes = new HashSet<Type>();
            var configSection = ConfigurationManager.GetSection("glimpse") as Section;
            if (configSection != null)
            {
                foreach (var t in configSection.RuntimePolicies.IgnoredTypes.Cast<TypeElement>().Select(te => te.Type))
                {
                    ignoredTypes.Add(t); 
                }
            }

            string dir = configSection.DiscoveryLocation;
            if (String.IsNullOrEmpty(dir))
            {
                var setupInformation = AppDomain.CurrentDomain.SetupInformation;
                if (!string.Equals(setupInformation.ShadowCopyFiles, "true", StringComparison.OrdinalIgnoreCase))
                {
                    dir = AppDomain.CurrentDomain.BaseDirectory;
                }
                else
                {
                    dir = Path.Combine(setupInformation.CachePath, setupInformation.ApplicationName);
                }
            }

            // "To eliminate all start up assembly scanning you'll want to return collections when GetAllInstances<T>() is called with a T of: IClientScript, IInspector, IResource, ITab, IDisplay, IRuntimePolicy and ISerializationConverter."
            var results = new List<T>();
            foreach (string f in Directory.GetFiles(dir, "Glimpse*.dll", SearchOption.AllDirectories))
            {
                var assembly = Assembly.LoadFrom(f);
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException exception)
                {
                    types = exception.Types.Where(x => x != null).ToArray();
                }

                results.AddRange(types
                    .Where(t => ((typeof(T).IsAssignableFrom(t) && !t.IsInterface) && !t.IsAbstract) && !ignoredTypes.Contains(t))
                    .Select(t => (T)Activator.CreateInstance(t)));
            }

            return results;
        }

        public T GetInstance<T>() where T : class
        {
            return Container.Kernel.TryGet<T>();
        }
    }
}