// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Licenses;

namespace NuGet.Services.Licenses
{
    public class LicenseExpressionSegmentator : ILicenseExpressionSegmentator
    {
        /// <summary>
        /// Specifies the max license expression tree depth, so we don't run out of stack while trying to traverse it.
        /// </summary>
        /// <remarks>
        /// Gallery limits the length of a license expression to 500 characters, which with the shortest sequence of glue (" OR ", spaces are required)
        /// and [currently non-existent] single character license ID gives us about 100 levels in a tree. We'll double it for the purpose of limiting
        /// the traverse depth to be on a safer side.
        /// </remarks>
        private const int MaxExpressionTreeDepth = 200;

        /// <summary>
        /// Does an in-order traversal of a license expression tree restoring the sequence of tokens
        /// used in the expression (omitting all parentheses and whitespace)
        /// </summary>
        /// <param name="licenseExpressionRoot">Root of the license expression tree</param>
        /// <returns>The list of license expression token in the order they appeared in the original expression.</returns>
        public List<CompositeLicenseExpressionSegment> GetLicenseExpressionSegments(NuGetLicenseExpression licenseExpressionRoot)
        {
            if (licenseExpressionRoot == null)
            {
                throw new ArgumentNullException(nameof(licenseExpressionRoot));
            }

            var segmentList = new List<CompositeLicenseExpressionSegment>();
            TraverseExpressionTreeInOrder(licenseExpressionRoot, segmentList);
            return segmentList;
        }

        /// <summary>
        /// Given the original license expression and list of segments produced by <see cref="GetLicenseExpressionSegments"/>
        /// produces full split of the expression into list of segments that include all the characters of the original
        /// license expression (including the parentheses and whitespace).
        /// </summary>
        /// <param name="licenseExpression">Original license expression.</param>
        /// <param name="segments">List of segments produced by <see cref="GetLicenseExpressionSegments"/></param>
        /// <returns>List of segments including the characters that are lost during expression parsing</returns>
        /// <remarks>
        /// The algorithm:
        /// Given the original license expression and the list of "meaningful" segments in this expression in
        /// the order they appear in the original expression, let startIndex = 0 be the character index in
        /// the license expression.
        /// 
        /// licenseExpression: "((MIT+))"
        ///           startIndex^
        /// segments: "MIT", "+"
        /// 
        /// Processing each segment from the segment list in order, we search where this segment first appears
        /// in the original license expression starting from startIndex. If not found then error.
        /// 
        /// If found, we have two cases:
        /// 1. found index is greater than startIndex
        /// licenseExpression: "((MIT+))"
        ///           startIndex^ ^found segment ("MIT") startIndex
        /// we need to emit "Other" segment containing sequence of characters between startIndex and found index
        /// - those characters were consumed by parses and don't appear explicitly in license expression. Then we
        /// can emit the "meaningful" segment ("MIT") itself and advance startIndex to the first character after it.
        /// 
        /// 2. found index is equal to startIndex
        /// licenseExpression: "((MIT+))"
        ///                startIndex^ and found segment ("+") startIndex
        /// in this case we can just emit the "meaningful" segment and advance startIndex to the first character
        /// after it.
        /// 
        /// If after processing all "Meaningful" segments we have startIndex pointing somewhere withing the limits
        /// of the original license expression string
        /// licenseExpression: "((MIT+))"
        ///                 startIndex^
        /// we emit the remaining part as other ("))" in this example)
        /// 
        /// So we end up with the following segments:
        /// 
        /// Other("(("), License("MIT"), Operator("+"), Other("))")
        /// </remarks>
        public List<CompositeLicenseExpressionSegment> SplitFullExpression(string licenseExpression, IReadOnlyCollection<CompositeLicenseExpressionSegment> segments)
        {
            if (licenseExpression == null)
            {
                throw new ArgumentNullException(nameof(licenseExpression));
            }

            if (segments == null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            var fullSegmentList = new List<CompositeLicenseExpressionSegment>();
            var startIndex = 0;
            foreach (var segment in segments)
            {
                var currentSegmentStartIndex = licenseExpression.IndexOf(segment.Value, startIndex);
                if (currentSegmentStartIndex < 0)
                {
                    throw new InvalidOperationException($"Unable to find '{segment.Value}' portion of the license expression starting from {startIndex} in '{licenseExpression}'");
                }
                if (currentSegmentStartIndex > startIndex)
                {
                    fullSegmentList.Add(
                        new CompositeLicenseExpressionSegment(licenseExpression.Substring(startIndex, currentSegmentStartIndex - startIndex),
                        CompositeLicenseExpressionSegmentType.Other));
                }
                fullSegmentList.Add(segment);
                startIndex = currentSegmentStartIndex + segment.Value.Length;
            }

            if (startIndex < licenseExpression.Length)
            {
                fullSegmentList.Add(
                    new CompositeLicenseExpressionSegment(licenseExpression.Substring(startIndex),
                    CompositeLicenseExpressionSegmentType.Other));
            }

            return fullSegmentList;
        }

        private static void TraverseExpressionTreeInOrder(NuGetLicenseExpression root, List<CompositeLicenseExpressionSegment> segmentList)
        {
            TraverseExpressionTreeInOrder(root, segmentList, 0);

            void TraverseExpressionTreeInOrder(NuGetLicenseExpression currentRoot, List<CompositeLicenseExpressionSegment> segments, int depth)
            {
                if (depth >= MaxExpressionTreeDepth)
                {
                    throw new InvalidOperationException($"NuGet license expression tree exceeds the max depth limit of {MaxExpressionTreeDepth}");
                }

                switch (currentRoot.Type)
                {
                    case LicenseExpressionType.License:
                        {
                            var licenseNode = (NuGetLicense)currentRoot;
                            segments.Add(new CompositeLicenseExpressionSegment(licenseNode.Identifier, CompositeLicenseExpressionSegmentType.LicenseIdentifier));
                            if (licenseNode.Plus)
                            {
                                segments.Add(new CompositeLicenseExpressionSegment("+", CompositeLicenseExpressionSegmentType.Operator));
                            }
                        }
                        break;

                    case LicenseExpressionType.Operator:
                        {
                            var operatorNode = (LicenseOperator)currentRoot;
                            if (operatorNode.OperatorType == LicenseOperatorType.LogicalOperator)
                            {
                                var logicalOperator = (LogicalOperator)operatorNode;
                                TraverseExpressionTreeInOrder(logicalOperator.Left, segments, depth + 1);
                                segments.Add(new CompositeLicenseExpressionSegment(GetLogicalOperatorString(logicalOperator), CompositeLicenseExpressionSegmentType.Operator));
                                TraverseExpressionTreeInOrder(logicalOperator.Right, segments, depth + 1);

                            }
                            else if (operatorNode.OperatorType == LicenseOperatorType.WithOperator)
                            {
                                var withOperator = (WithOperator)operatorNode;
                                TraverseExpressionTreeInOrder(withOperator.License, segments, depth + 1);
                                segments.Add(new CompositeLicenseExpressionSegment("WITH", CompositeLicenseExpressionSegmentType.Operator));
                                segments.Add(new CompositeLicenseExpressionSegment(withOperator.Exception.Identifier, CompositeLicenseExpressionSegmentType.ExceptionIdentifier));
                            }
                            else
                            {
                                throw new InvalidOperationException($"Unknown operator type: {operatorNode.OperatorType}");
                            }
                        }
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown node type: {currentRoot.Type}");
                }
            }
        }

        private static string GetLogicalOperatorString(LogicalOperator logicalOperator)
        {
            switch (logicalOperator.LogicalOperatorType)
            {
                case LogicalOperatorType.And:
                    return "AND";

                case LogicalOperatorType.Or:
                    return "OR";

                default:
                    throw new InvalidOperationException($"Unsupported logical operator type: {logicalOperator.LogicalOperatorType}");
            }
        }
    }
}