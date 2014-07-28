using Resolver.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resolver.Resolver
{
    class Participants
    {
        public static List<Package>[] Collect(PNode pnode)
        {
            return SortParticipants(GetParticipants(pnode));
        }

        private static List<Package>[] SortParticipants(IDictionary<string, ISet<Package>> participants)
        {
            List<Package>[] result = new List<Package>[participants.Count];

            int i = 0;

            foreach (KeyValuePair<string, ISet<Package>> participant in participants)
            {
                List<Package> versions = new List<Package>(participant.Value);

                List<Package> candidates = new List<Package>();
                foreach (Package version in versions.OrderBy(v => v.Version, SemanticVersionRange.DefaultComparer))
                {
                    candidates.Add(version);
                }

                result[i++] = candidates;
            }

            return result;
        }

        private static IDictionary<string, ISet<Package>> GetParticipants(PNode pnode)
        {
            IDictionary<string, ISet<Package>> participants = new Dictionary<string, ISet<Package>>();
            foreach (PVNode child in pnode.Children)
            {
                GetParticipants(child, participants);
            }
            return participants;
        }

        private static void GetParticipants(PVNode pvnode, IDictionary<string, ISet<Package>> participants)
        {
            foreach (PNode child in pvnode.Children)
            {
                GetParticipants(child, participants);
            }
        }

        private static void GetParticipants(PNode pnode, IDictionary<string, ISet<Package>> participants)
        {
            foreach (PVNode child in pnode.Children)
            {
                GetParticipants(child, pnode.Id, participants);
            }
        }

        private static void GetParticipants(PVNode pvnode, string id, IDictionary<string, ISet<Package>> participants)
        {
            ISet<Package> versions;
            if (!participants.TryGetValue(id, out versions))
            {
                versions = new HashSet<Package>();
                participants.Add(id, versions);
            }
            versions.Add(pvnode.Package);

            foreach (PNode child in pvnode.Children)
            {
                GetParticipants(child, participants);
            }
        }
    }
}
