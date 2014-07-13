using System;
using System.Collections.Generic;
using System.Linq;
using NuGet;

namespace NuGetGallery
{
    public class VerifyPackageRequest
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string LicenseUrl { get; set; }

        public bool Listed { get; set; }
        public EditPackageVersionRequest Edit { get; set; }
        public Version MinClientVersion { get; set; }
        public string Language { get; set; }

        public IEnumerable<NuGet.PackageDependencySet> DependencySets { get; set; }
        public IEnumerable<FrameworkAssemblyReference> FrameworkAssemblies { get; set; }

        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> FrameworkAssembliesBySupportedFrameworks
        {
            get
            {
                if (FrameworkAssemblies == null || FrameworkAssemblies.Any() == false)
                    return new List<KeyValuePair<string, IEnumerable<string>>>();

                var resultList = new List<KeyValuePair<string, IEnumerable<string>>>();

                foreach (var frameworkAssemblyReference in FrameworkAssemblies)
                {
                    if (frameworkAssemblyReference.SupportedFrameworks != null)
                    {
                        foreach (var supportedFramework in frameworkAssemblyReference.SupportedFrameworks)
                        {
                            if (resultList.Any(x => x.Key == supportedFramework.FullName))
                            {
                                var frameworkAlreadyInResultlist = resultList.Single();
                                resultList.Remove(frameworkAlreadyInResultlist);

                                var assemblyList = new List<string> { frameworkAssemblyReference.AssemblyName };
                                assemblyList.AddRange(frameworkAlreadyInResultlist.Value);

                                var groupedElement = new KeyValuePair<string, IEnumerable<string>>(supportedFramework.FullName, assemblyList);
                                resultList.Add(groupedElement);
                            }
                            else
                            {
                                var assemblyList = new List<string> { frameworkAssemblyReference.AssemblyName };
                                var groupedElement = new KeyValuePair<string, IEnumerable<string>>(supportedFramework.FullName, assemblyList);
                                resultList.Add(groupedElement);
                            }

                        }
                    }
                }

                return resultList;
            }
        }

    }
}