using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static NuGetGallery.DependencySetsViewModel;

namespace NuGetGallery.ViewModels
{
    public class VulnerableDependencySetsViewModel
    {
        public VulnerableDependencySetsViewModel(IEnumerable<VulnerableDependencyModel> vulnerableDependencyModels) {
            VulnerableDependencySets = new Dictionary<string, IEnumerable<VulnerableDependencyModel>>();

            var vulnerabilitiesGroupedBySeverity = vulnerableDependencyModels.GroupBy(d => d.VulnerabilitySeverity);

            foreach (var vulnerabilityGroup in vulnerabilitiesGroupedBySeverity)
            {
                var vulnerabilitySeverityKey = vulnerabilityGroup.Key;

                if (!VulnerableDependencySets.ContainsKey(vulnerabilitySeverityKey))
                {
                    VulnerableDependencySets.Add(vulnerabilitySeverityKey, vulnerabilityGroup.OrderBy(x => x.PackageId));
                }
            }

            VulnerableDependencySets = VulnerableDependencySets.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }

        public IDictionary<string, IEnumerable<VulnerableDependencyModel>> VulnerableDependencySets = new Dictionary<string, IEnumerable<VulnerableDependencyModel>>();
    }
}