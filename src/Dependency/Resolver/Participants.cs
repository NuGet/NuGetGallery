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
        public static List<Tuple<string, SemanticVersion>>[] Collect(PNode pnode)
        {
            return SortParticipants(GetParticipants(pnode));
        }

        private static List<Tuple<string, SemanticVersion>>[] SortParticipants(IDictionary<string, ISet<SemanticVersion>> participants)
        {
            List<Tuple<string, SemanticVersion>>[] result = new List<Tuple<string, SemanticVersion>>[participants.Count];

            int i = 0;

            foreach (KeyValuePair<string, ISet<SemanticVersion>> participant in participants)
            {
                List<SemanticVersion> versions = new List<SemanticVersion>(participant.Value);
                versions.Sort(SemanticVersionRange.DefaultComparer);

                List<Tuple<string, SemanticVersion>> candidates = new List<Tuple<string, SemanticVersion>>();
                foreach (SemanticVersion version in versions)
                {
                    candidates.Add(new Tuple<string, SemanticVersion>(participant.Key, version));
                }

                result[i++] = candidates;
            }

            return result;
        }

        private static IDictionary<string, ISet<SemanticVersion>> GetParticipants(PNode pnode)
        {
            IDictionary<string, ISet<SemanticVersion>> participants = new Dictionary<string, ISet<SemanticVersion>>();
            foreach (PVNode child in pnode.Children)
            {
                GetParticipants(child, participants);
            }
            return participants;
        }

        private static void GetParticipants(PVNode pvnode, IDictionary<string, ISet<SemanticVersion>> participants)
        {
            foreach (PNode child in pvnode.Children)
            {
                GetParticipants(child, participants);
            }
        }

        private static void GetParticipants(PNode pnode, IDictionary<string, ISet<SemanticVersion>> participants)
        {
            foreach (PVNode child in pnode.Children)
            {
                GetParticipants(child, pnode.Id, participants);
            }
        }

        private static void GetParticipants(PVNode pvnode, string id, IDictionary<string, ISet<SemanticVersion>> participants)
        {
            ISet<SemanticVersion> versions;
            if (!participants.TryGetValue(id, out versions))
            {
                versions = new HashSet<SemanticVersion>();
                participants.Add(id, versions);
            }
            versions.Add(pvnode.Version);

            foreach (PNode child in pvnode.Children)
            {
                GetParticipants(child, participants);
            }
        }
    }
}
