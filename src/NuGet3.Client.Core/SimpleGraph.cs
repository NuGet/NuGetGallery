using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet3.Client
{
    public class SimpleGraph
    {
        private Dictionary<string, Dictionary<string, HashSet<string>>> _spoIndex;
        private Dictionary<string, Dictionary<string, HashSet<string>>> _posIndex;
        private Dictionary<string, Dictionary<string, HashSet<string>>> _ospIndex;

        public SimpleGraph()
        {
            _spoIndex = new Dictionary<string, Dictionary<string, HashSet<string>>>();
            _posIndex = new Dictionary<string, Dictionary<string, HashSet<string>>>();
            _ospIndex = new Dictionary<string, Dictionary<string, HashSet<string>>>();
        }

        public void Add(string subj, string pred, string obj) {
            AddToIndex(_spoIndex, subj, pred, obj);
            AddToIndex(_posIndex, pred, obj, subj);
            AddToIndex(_ospIndex, obj, subj, pred);
        }

        private void AddToIndex(Dictionary<string, Dictionary<string, HashSet<string>>> index, string a, string b, string c)
        {
            if (!index.ContainsKey(a))
            {
                index[a] = new Dictionary<string, HashSet<string>> { {b, new HashSet<string> {c}}};
            }
            else
            {
                if (!index[a].ContainsKey(b))
                {
                    var set = new HashSet<string>();
                    set.Add(c);
                    index[a][b] = set;
                }
                else
                {
                    index[a][b].Add(c);
                }
            }
        }

        public void Remove(string subj, string pred, string obj)
        {
            foreach (var triple in Triples(subj, pred, obj))
            {
                RemoveFromIndex(_spoIndex, triple.Item1, triple.Item2, triple.Item3);
            }
        }

        private void RemoveFromIndex(Dictionary<string, Dictionary<string, HashSet<string>>> index, string a, string b, string c)
        {
            try
            {
                var bs = index[a];
                var cset = bs[b];
                cset.Remove(c);
                if (cset.Count == 0) bs.Remove(b);
                if (bs.Count == 0) index.Remove(a);
            }
            catch (KeyNotFoundException)
            {
                return;
            }
        }

        public IEnumerable<Tuple<string, string, string>> Triples(string subj, string pred, string obj)
        {
            var enumerator = TriplesInternal(subj, pred, obj).GetEnumerator();
            while (true)
            {
                Tuple<string, string, string> ret = null;
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }
                    ret = enumerator.Current;
                }
                catch (KeyNotFoundException)
                {
                    break;
                }
                yield return ret;
            }
        }

        private IEnumerable<Tuple<string, string, string>> TriplesInternal(string subj, string pred, string obj)
        {
            if (subj != null)
            {
                if (pred != null)
                {
                    if (obj != null)
                    {
                        if (_spoIndex[subj][pred].Contains(obj))
                        {
                            yield return Tuple.Create(subj, pred, obj);
                        }
                    }
                    else
                    {
                        foreach (string retObj in _spoIndex[subj][pred])
                        {
                            yield return Tuple.Create(subj, pred, retObj);
                        }
                    }
                }
                else
                {
                    if (obj != null)
                    {
                        foreach (string retPred in _ospIndex[obj][subj])
                        {
                            yield return Tuple.Create(subj, retPred, obj);
                        }
                    }
                    else
                    {
                        foreach (var item in _spoIndex[subj])
                        {
                            var retPred = item.Key;
                            var objSet = item.Value;
                            foreach (var retObj in objSet)
                            {
                                yield return Tuple.Create(subj, retPred, retObj);
                            }
                        }
                    }
                }
            }
            else
            {
                if (pred != null)
                {
                    if (obj != null)
                    {
                        foreach (var retSubj in _posIndex[pred][obj])
                        {
                            yield return Tuple.Create(retSubj, pred, obj);
                        }
                    }
                    else
                    {
                        foreach (var item in _posIndex[pred])
                        {
                            var retObj = item.Key;
                            var subjSet = item.Value;
                            foreach (var retSubj in subjSet)
                            {
                                yield return Tuple.Create(retSubj, pred, retObj);
                            }
                        }
                    }
                }
                else
                {
                    if (obj != null)
                    {
                        foreach (var item in _ospIndex[obj])
                        {
                            var retSubj = item.Key;
                            var predSet = item.Value;
                            foreach (var retPred in predSet)
                            {
                                yield return Tuple.Create(retSubj, retPred, obj);
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in _spoIndex)
                        {
                            var retSubj = item.Key;
                            var predSet = item.Value;
                            foreach (var item2 in predSet)
                            {
                                var retPred = item2.Key;
                                var objSet = item2.Value;
                                foreach (var retObj in objSet)
                                {
                                    yield return Tuple.Create(retSubj, retPred, retObj);
                                }
                            }
                        }
                    }
                }
            }
        }

        public string Value(string subj, string pred, string obj)
        {
            foreach (var val in Triples(subj, pred, obj))
            {
                if (subj != null)
                {
                    return val.Item1;
                }
                if (pred != null)
                {
                    return val.Item2;
                }
                if (obj != null)
                {
                    return val.Item3;
                }
            }
            return null;
        }

        public List<Dictionary<string, string>> Query(List<Tuple<string, string, string>> clauses)
        {
            List<Dictionary<string, string>> bindings = null;

            foreach (var clauseTuple in clauses)
            {
                var clause = new[] { clauseTuple.Item1, clauseTuple.Item2, clauseTuple.Item3 };
                var bpos = new Dictionary<string, int>();
                var qc = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    if (clause[i].StartsWith("?"))
                    {
                        qc.Add(null);
                        bpos[clause[i].Substring(1)] = i;
                    }
                    else
                    {
                        qc.Add(clause[i]);
                    }
                }
                var rows = Triples(qc[0], qc[1], qc[2]);

                if (bindings == null)
                {
                    bindings = new List<Dictionary<string, string>>();

                    foreach (var row in rows)
                    {
                        var binding = new Dictionary<string, string>();
                        foreach (var item in bpos)
                        {
                            binding[item.Key] = new[] { row.Item1, row.Item2, row.Item3 }[item.Value];
                            bindings.Add(binding);
                        }
                    }
                }
                else
                {
                    var newb = new List<Dictionary<string,string>>();
                    foreach (var binding in bindings)
                    {
                        foreach (var row in rows)
                        {
                            bool validmatch = true;
                            var tempbinding = new Dictionary<string, string>(binding);
                            foreach (var item in bpos)
                            {
                                if (tempbinding.ContainsKey(item.Key))
                                {
                                    if (tempbinding[item.Key] != new[]{row.Item1,row.Item2,row.Item3}[item.Value])
                                    {
                                        validmatch = false;
                                    }
                                }
                                else
                                {
                                    tempbinding[item.Key] = new[] { row.Item1, row.Item2, row.Item3 }[item.Value];
                                }
                            }
                            if (validmatch)
                            {
                                newb.Add(tempbinding);
                            }
                        }
                    }
                    bindings = newb;
                }
            }

            return bindings;
        }
    }
}
