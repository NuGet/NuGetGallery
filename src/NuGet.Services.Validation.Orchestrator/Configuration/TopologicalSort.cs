// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace NuGet.Services.Validation.Orchestrator
{
    public static class TopologicalSort
    {
        /// <summary>
        /// Processors cannot run in parallel with other processors or even with other validators. Suppose you have the
        /// following validator graph (where --> indicates a validator dependent)
        /// 
        ///                   ---> Validator B ---
        ///                 /                      \
        /// Validator A ---                          ---> Validator D
        ///                 \                      /
        ///                   ---> Validator C ---
        /// 
        /// In this case, B and C cannot be processors. A and D can. Given a graph of validator dependencies and the
        /// list of validators that are processors, we use the following algorithm to determine whether it is possible
        /// for a processor to run in parallel with anything else.
        /// 
        /// 1. Enumerate all valid orderings using topological sort. In our example above, this would be:
        ///    - A B C D
        ///    - A C B D
        /// 2. For each processor, verify that the position in all orderings is the same.
        ///    - Note that A is always first and D is always fourth.
        /// 
        /// This allows us to verify that a validator configuration is safe before orchestrator even starts accepting
        /// validation messages.
        /// </summary>
        /// <param name="validators">The validator configuration items.</param>
        /// <param name="cannotBeParallel">The names of validators that are also processors.</param>
        /// <exception cref="ConfigurationErrorsException">
        /// Thrown if a cycle or parallel processor is found
        /// </exception>
        public static void Validate(IReadOnlyList<ValidationConfigurationItem> validators, IReadOnlyList<string> cannotBeParallel)
        {
            var allOrders = EnumerateAll(validators);
            if (!allOrders.Any())
            {
                throw new ConfigurationErrorsException("No validation sequences were found. This indicates a cycle in the validation dependencies.");
            }

            // A dictionary mapping the name of the validator to its index in the first topological sort result. All
            // other results must have their processors at the same indexes. If this is true, that means that no
            // validators or processors can run in parallel with any processor.
            var nameToExpectedIndex = allOrders[0]
                .Select((x, i) => new { Name = x, Index = i })
                .ToDictionary(x => x.Name, x => x.Index);

            foreach (var order in allOrders.Skip(1))
            {
                foreach (var name in cannotBeParallel)
                {
                    var index = nameToExpectedIndex[name];
                    var otherName = order[index];
                    if (otherName != name)
                    {
                        throw new ConfigurationErrorsException(
                            $"The processor {name} could run in parallel with {otherName}. Processors must not run " +
                            $"in parallel with any other validators.");
                    }
                }
            }
        }

        public static List<List<string>> EnumerateAll(IReadOnlyList<ValidationConfigurationItem> validators)
        {
            // Build the graph.
            var graph = validators.ToDictionary(x => x.Name, x => new ValidatorNode(x.Name));

            // Invert the node relationship. Validators specify what they depend on. Nodes in a directed graph to be
            // explored using topological sort should specify what their dependents are.
            foreach (var validator in validators)
            {
                foreach (var dependencyName in validator.RequiredValidations)
                {
                    graph[validator.Name].InDegree++;
                    graph[dependencyName].DependentValidations.Add(validator.Name);
                }
            }

            // Enumerate all combindations.
            var allResults = new List<List<string>>();
            AllTopologicalSort(graph, new List<string>(), allResults);

            return allResults;
        }

        /// <summary>
        /// Executes topological sort on the provided graph of validators. All possible results are enumerated and
        /// returned.
        /// </summary>
        /// <remarks>
        /// Source: https://www.geeksforgeeks.org/all-topological-sorts-of-a-directed-acyclic-graph/
        /// </remarks>
        private static void AllTopologicalSort(
            IReadOnlyDictionary<string, ValidatorNode> graph,
            List<string> currentResult,
            List<List<string>> allResults)
        {
            var done = false;

            foreach (var node in graph.Values)
            {
                if (node.InDegree == 0 && !node.Visited)
                {
                    foreach (var dependencyName in node.DependentValidations)
                    {
                        graph[dependencyName].InDegree--;
                    }

                    currentResult.Add(node.Name);
                    node.Visited = true;

                    // Recurse.
                    AllTopologicalSort(graph, currentResult, allResults);

                    node.Visited = false;
                    currentResult.RemoveAt(currentResult.Count - 1);

                    foreach (var dependencyName in node.DependentValidations)
                    {
                        graph[dependencyName].InDegree++;
                    }

                    done = true;
                }
            }

            if (!done && currentResult.Count == graph.Count)
            {
                // Append a copy of the running result.
                allResults.Add(currentResult.ToList());
            }
        }

        private class ValidatorNode
        {
            public ValidatorNode(string name)
            {
                Name = name ?? throw new ArgumentNullException(nameof(name));
            }

            public string Name { get; }
            public List<string> DependentValidations { get; } = new List<string>();
            public int InDegree { get; set; }
            public bool Visited { get; set; }
        }
    }
}
