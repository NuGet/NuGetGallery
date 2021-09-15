// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NuGetGallery
{
    public static class TyposquattingDistanceCalculation
    {
        private const char PlaceholderForAlignment = '*';  // This const place holder variable is used for strings alignment

        private static readonly HashSet<char> SpecialCharacters = new HashSet<char> { '.', '_', '-' };
        private static readonly string SpecialCharactersToString = "[" + new string(SpecialCharacters.ToArray()) + "]";

        private class BasicEditDistanceInfo
        {
            public int Distance { get; set; }
            public PathInfo[,] Path { get; set; }
        }

        private enum PathInfo
        {
            Match,
            Delete,
            Substitute,
            Insert,
        }

        public static bool IsDistanceLessOrEqualThanThreshold(string str1, string str2, int threshold)
        {
            if (str1 == null)
            {
                throw new ArgumentNullException(nameof(str1));
            }
            if (str2 == null)
            {
                throw new ArgumentNullException(nameof(str2));
            }

            var newStr1 = RegexEx.ReplaceWithTimeout(str1, SpecialCharactersToString, string.Empty, RegexOptions.None);
            var newStr2 = RegexEx.ReplaceWithTimeout(str2, SpecialCharactersToString, string.Empty, RegexOptions.None);
            if (Math.Abs(newStr1.Length - newStr2.Length) > threshold)
            {
                return false;
            }

            return GetDistance(str1, str2, threshold) <= threshold;
        }

        private static int GetDistance(string str1, string str2, int threshold)
        {
            var basicEditDistanceInfo = GetBasicEditDistanceWithPath(str1, str2);
            if (basicEditDistanceInfo.Distance <= threshold)
            {
                return basicEditDistanceInfo.Distance;
            }
            var alignedStrings = TraceBackAndAlignStrings(basicEditDistanceInfo.Path, str1, str2);
            var refreshedEditDistance = RefreshEditDistance(alignedStrings[0], alignedStrings[1], basicEditDistanceInfo.Distance);

            return refreshedEditDistance;
        }

        /// <summary>
        /// The following function is used to calculate the classical edit distance and construct the path in dynamic programming way.
        /// </summary>
        private static BasicEditDistanceInfo GetBasicEditDistanceWithPath(string str1, string str2)
        {
            var distances = new int[str1.Length + 1, str2.Length + 1];
            var path = new PathInfo[str1.Length + 1, str2.Length + 1];
            distances[0, 0] = 0;
            path[0, 0] = PathInfo.Match;
            for (var i = 1; i <= str1.Length; i++)
            {
                distances[i, 0] = i;
                path[i, 0] = PathInfo.Delete;
            }

            for (var j = 1; j <= str2.Length; j++)
            {
                distances[0, j] = j;
                path[0, j] = PathInfo.Insert;
            }

            for (var i = 1; i <= str1.Length; i++)
            {
                for (var j = 1; j <= str2.Length; j++)
                {
                    if (str1[i - 1] == str2[j - 1])
                    {
                        distances[i, j] = distances[i - 1, j - 1];
                        path[i, j] = PathInfo.Match;
                    }
                    else
                    {
                        distances[i, j] = distances[i - 1, j - 1] + 1;
                        path[i, j] = PathInfo.Substitute;

                        if (distances[i - 1, j] + 1 < distances[i, j])
                        {
                            distances[i, j] = distances[i - 1, j] + 1;
                            path[i, j] = PathInfo.Delete;
                        }

                        if (distances[i, j - 1] + 1 < distances[i, j])
                        {
                            distances[i, j] = distances[i, j - 1] + 1;
                            path[i, j] = PathInfo.Insert;
                        }
                    }
                }
            }

            return new BasicEditDistanceInfo
            {
                Distance = distances[str1.Length, str2.Length],
                Path = path
            };
        }

        /// <summary>
        /// The following function is used to traceback based on the construction path and align two strings.
        /// Example:  For two strings: "asp.net" "aspnet". After traceback and alignment, we will have aligned strings as "asp.net" "asp*net" ('*' is the placeholder).
        /// The returned strings contain the two inputted strings after alignment. 
        /// </summary>
        private static string[] TraceBackAndAlignStrings(PathInfo[,] path, string str1, string str2)
        {
            var newStr1 = new StringBuilder(str1);
            var newStr2 = new StringBuilder(str2);
            var alignedStrs = new string[2];

            var i = str1.Length;
            var j = str2.Length;
            while (i > 0 && j > 0)
            {
                switch (path[i, j])
                {
                    case PathInfo.Match:
                        i--;
                        j--;
                        break;
                    case PathInfo.Substitute:
                        i--;
                        j--;
                        break;
                    case PathInfo.Delete:
                        newStr2.Insert(j, PlaceholderForAlignment);
                        i--;
                        break;
                    case PathInfo.Insert:
                        newStr1.Insert(i, PlaceholderForAlignment);
                        j--;
                        break;
                    default:
                        throw new ArgumentException("Invalidate operation for edit distance trace back: " + path[i, j]);
                }
            }

            for (var k = 0; k < i; k++)
            {
                newStr2.Insert(k, PlaceholderForAlignment);
            }

            for (var k = 0; k < j; k++)
            {
                newStr1.Insert(k, PlaceholderForAlignment);
            }

            alignedStrs[0] = newStr1.ToString();
            alignedStrs[1] = newStr2.ToString();

            return alignedStrs;
        }

        /// <summary>
        /// The following function is used to refresh the edit distance based on predefined rules. (Insert/Delete special characters will not account for distance)
        /// Example:  For two aligned strings: "asp.net" "asp*net" ('*' is the placeholder), we will scan the two strings again and the mapping from '.' to '*' will not account for the distance.
        ///           So the final distance will be 0 for these two strings "asp.net" "aspnet".
        /// </summary>
        private static int RefreshEditDistance(string alignedStr1, string alignedStr2, int basicEditDistance)
        {
            if (alignedStr1.Length != alignedStr2.Length)
            {
                throw new ArgumentException("The lengths of two aligned strings are not same!");
            }

            var sameSubstitution = 0;
            for (var i = 0; i < alignedStr2.Length; i++)
            {
                if (alignedStr1[i] != alignedStr2[i])
                {
                    if (alignedStr1[i] == PlaceholderForAlignment && SpecialCharacters.Contains(alignedStr2[i]))
                    {
                        sameSubstitution += 1;
                    }
                    else if (alignedStr2[i] == PlaceholderForAlignment && SpecialCharacters.Contains(alignedStr1[i]))
                    {
                        sameSubstitution += 1;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            return basicEditDistance - sameSubstitution;
        }        
    }
}