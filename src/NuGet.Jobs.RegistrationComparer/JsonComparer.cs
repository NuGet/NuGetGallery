// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace NuGet.Jobs.RegistrationComparer
{
    public class JsonComparer
    {
        public void Compare(JToken left, JToken right, ComparisonContext context)
        {
            var normalized = false;
            if (left.Type != right.Type)
            {
                Type leftType = null;
                Type rightType = null;
                try
                {
                    foreach (var pair in context.Normalizers.ScalarNormalizers)
                    {
                        if (pair.Key(left.Path))
                        {
                            leftType = pair.Value(left, true, context)?.GetType();
                            rightType = pair.Value(right, false, context)?.GetType();
                            normalized = true;
                            break;
                        }
                    }
                }
                catch
                {
                    // Swallow exceptions encountered during normalization. The comparison will just consider the tokens
                    // as different, which is correct.
                }

                if (!normalized || leftType != rightType)
                {
                    throw new InvalidOperationException(Environment.NewLine +
                        $"The type of the JSON value is different." + Environment.NewLine +
                        $"|  Left URL:   {context.LeftUrl}" + Environment.NewLine +
                        $"|  Right URL:  {context.RightUrl}" + Environment.NewLine +
                        $"|  Left path:  {left.Path}" + Environment.NewLine +
                        $"|  Right path: {right.Path}" + Environment.NewLine +
                        $"|  Left type:  {left.Type}" + Environment.NewLine +
                        $"|  Right type: {right.Type}" + Environment.NewLine);
                }
            }

            if (!normalized && left.Type == JTokenType.Object)
            {
                Compare((JObject)left, (JObject)right, context);
            }
            else if (!normalized && left.Type == JTokenType.Array)
            {
                Compare((JArray)left, (JArray)right, context);
            }
            else
            {
                var leftJson = left.ToString();
                var rightJson = right.ToString();
                if (leftJson != rightJson)
                {
                    var leftString = leftJson;
                    var rightString = rightJson;
                    foreach (var pair in context.Normalizers.ScalarNormalizers)
                    {
                        if (pair.Key(left.Path))
                        {
                            leftString = pair.Value(left, true, context) ?? leftJson;
                            rightString = pair.Value(right, false, context) ?? rightJson;
                            break;
                        }
                    }

                    if (leftString != rightString)
                    {
                        throw new InvalidOperationException(Environment.NewLine +
                           $"The value of the JSON scalar is different." + Environment.NewLine +
                           $"|  Left URL:    {context.LeftUrl}" + Environment.NewLine +
                           $"|  Right URL:   {context.RightUrl}" + Environment.NewLine +
                           $"|  Left path:   {left.Path}" + Environment.NewLine +
                           $"|  Right path:  {right.Path}" + Environment.NewLine +
                           $"|  Left value:  {leftJson}" + Environment.NewLine +
                           $"|  Right value: {rightJson}" + Environment.NewLine);
                    }
                }
            }
        }

        private void Compare(JArray left, JArray right, ComparisonContext context)
        {
            if (left.Count != right.Count)
            {
                throw new InvalidOperationException(Environment.NewLine +
                    $"The JSON array item count is different." + Environment.NewLine +
                    $"|  Left URL:    {context.LeftUrl}" + Environment.NewLine +
                    $"|  Right URL:   {context.RightUrl}" + Environment.NewLine +
                    $"|  Left path:   {left.Path}" + Environment.NewLine +
                    $"|  Right path:  {right.Path}" + Environment.NewLine +
                    $"|  Left count:  {left.Count}" + Environment.NewLine +
                    $"|  Right count: {right.Count}" + Environment.NewLine);
            }

            var leftItems = left.ToList();
            var rightItems = right.ToList();
            if (leftItems.Count > 1)
            {
                foreach (var pair in context.Normalizers.UnsortedArrays)
                {
                    if (pair.Key(left))
                    {
                        leftItems.Sort(pair.Value);
                        rightItems.Sort(pair.Value);
                        break;
                    }
                }
            }

            for (var i = 0; i < leftItems.Count; i++)
            {
                Compare(leftItems[i], rightItems[i], context);
            }
        }

        private void Compare(JObject left, JObject right, ComparisonContext context)
        {
            var leftPropertyNames = left.Properties().Select(x => x.Name);
            var rightPropertyNames = right.Properties().Select(x => x.Name);

            var onlyLeft = leftPropertyNames.Except(rightPropertyNames);
            var onlyRight = rightPropertyNames.Except(leftPropertyNames);
            if (onlyLeft.Any() || onlyRight.Any())
            {
                throw new InvalidOperationException(Environment.NewLine +
                    $"The JSON object property names are disjoint." + Environment.NewLine +
                    $"|  Left URL:   {context.LeftUrl}" + Environment.NewLine +
                    $"|  Right URL:  {context.RightUrl}" + Environment.NewLine +
                    $"|  Left path:  {left.Path}" + Environment.NewLine +
                    $"|  Right path: {right.Path}" + Environment.NewLine +
                    $"|  Only left:  {string.Join(", ", onlyLeft)}" + Environment.NewLine +
                    $"|  Only right: {string.Join(", ", onlyRight)}" + Environment.NewLine);
            }

            var leftProperties = left.Properties();
            var rightProperties = right.Properties();

            if (!leftPropertyNames.SequenceEqual(rightPropertyNames))
            {
                if (context.Normalizers.UnsortedObjects.Any(x => x(left.Path)))
                {
                    leftProperties = leftProperties.OrderBy(x => x.Name);
                    rightProperties = rightProperties.OrderBy(x => x.Name);
                }
                else
                {
                    throw new InvalidOperationException(Environment.NewLine +
                        $"The JSON object property names are in a different order." + Environment.NewLine +
                        $"|  Left URL:    {context.LeftUrl}" + Environment.NewLine +
                        $"|  Right URL:   {context.RightUrl}" + Environment.NewLine +
                        $"|  Left path:   {left.Path}" + Environment.NewLine +
                        $"|  Right path:  {right.Path}" + Environment.NewLine +
                        $"|  Left order:  {string.Join(", ", leftPropertyNames)}" + Environment.NewLine +
                        $"|  Right order: {string.Join(", ", rightPropertyNames)}" + Environment.NewLine);
                }
            }

            var pairs = leftProperties.Zip(rightProperties, (l, r) => new { Left = l.Value, Right = r.Value });
            foreach (var pair in pairs)
            {
                Compare(pair.Left, pair.Right, context);
            }
        }
    }
}
