// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using Xunit;

namespace NuGetGallery.Services
{
    public class StringReplaceTemplateProcessorFacts
    {
        [Fact]
        public void ConstructorAllowsTemplateToBeNull()
        {
            var target = new StringReplaceTemplateProcessor<object>(
                template: null,
                placeholderProcessors: new Dictionary<string, Func<object, string>>());

            var result = target.Process(new object());

            Assert.Null(result);
        }

        [Fact]
        public void ConstructorThrowsWhenPlaceholderProcessorsIsNull()
        {
            var ex = Assert.Throws<ArgumentNullException>(
                () => new StringReplaceTemplateProcessor<object>(
                    template: "",
                    placeholderProcessors: null));

            Assert.Equal("placeholderProcessors", ex.ParamName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ConstructorThrowsWhenPlaceholderIsInvalid(string placeholder)
        {
            // Dictionary<TKey, TValue> does not support null keys, so we need some creativity to test it
            var placeholdersMock = new Mock<IReadOnlyDictionary<string, Func<object, string>>>();
            IReadOnlyCollection<KeyValuePair<string, Func<object, string>>> mockCollection = new [] { Kvp<object>(null, _ => "") };

            placeholdersMock
                .Setup(p => p.GetEnumerator())
                .Returns(mockCollection.GetEnumerator());

            var ex = Assert.Throws<ArgumentException>(
                () => new StringReplaceTemplateProcessor<object>(
                    template: "",
                    placeholderProcessors: placeholdersMock.Object));

            Assert.Equal("placeholderProcessors", ex.ParamName);
        }

        [Fact]
        public void ConstructorAllowsWhitespaceOnlyPlaceholder()
        {
            var ex = Record.Exception(() => new StringReplaceTemplateProcessor<object>(
                template: "",
                placeholderProcessors: new Dictionary<string, Func<object, string>> { { " ", _ => " " } }));
            Assert.Null(ex);
        }

        [Fact]
        public void ConstructorThrowsWhenProcessorIsNull()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => new StringReplaceTemplateProcessor<object>(
                    template: "",
                    placeholderProcessors: new Dictionary<string, Func<object, string>>
                    {
                        { "something", null }
                    }));

            Assert.Equal("placeholderProcessors", ex.ParamName);
        }

        [Theory]
        [InlineData("ph")]
        [InlineData("phph")]
        public void CallsThePlaceholderProcessor(string template)
        {
            var inputObject = new object();
            bool called = false;

            var target = new StringReplaceTemplateProcessor<object>(
                template: template,
                placeholderProcessors: new Dictionary<string, Func<object, string>>
                {
                    { "ph", data => {Assert.Same(inputObject, data); called = true; return "123"; } }
                });

            target.Process(inputObject);

            Assert.True(called);
        }

        [Theory]
        [InlineData("", "1", "", "")]
        [InlineData("1", "1", "", "")]
        [InlineData("1", "1", "11", "11")]
        [InlineData("11", "1", "11", "1111")]
        [InlineData(" ", " ", "", "")]
        [InlineData("foo{A}bar", "{a}", "", "foo{A}bar")]
        [InlineData("foo{A}{B}bar", "{A}", "A", "fooA{B}bar")]
        public void ReplacesPlaceholders(string template, string placeholder, string substitution, string expectedResult)
        {
            var target = new StringReplaceTemplateProcessor<object>(template, new Dictionary<string, Func<object, string>> { { placeholder, _ => substitution } });

            var result = target.Process(input: null);

            Assert.Equal(expectedResult, result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(42)]
        public void ReplacesMultiplePlaceholders(int number)
        {
            string template = "Start";
            string expectedResult = template;
            var processors = new Dictionary<string, Func<object, string>>();
            for (var index = 0; index < number; ++index)
            {
                var placeholder = GeneratePlaceholder(index);
                template += placeholder;
                var value = index.ToString();
                processors.Add(placeholder, _ => value);
                expectedResult += value;
            }

            var target = new StringReplaceTemplateProcessor<object>(template, processors);
            var result = target.Process(null);

            Assert.Equal(expectedResult, result);
        }

        private string GeneratePlaceholder(int index)
        {
            var placeholder = new StringBuilder();
            do
            {
                int remainder = index % 26;
                index = index / 26;
                placeholder.Insert(0, (char)('A' + remainder));
            } while (index > 0);
            return $"{{{placeholder}}}";
        }

        private static KeyValuePair<string, Func<TInput, string>> Kvp<TInput>(string key, Func<TInput, string> value)
        {
            return new KeyValuePair<string, Func<TInput, string>>(key, value);
        }
    }
}
