// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using static Microsoft.AspNetCore.Razor.LanguageServer.CompletionItemExtensions;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultTagHelperDescriptionFactory : TagHelperDescriptionFactory
    {
        private static readonly Lazy<Regex> ExtractCrefRegex = new Lazy<Regex>(
            () => new Regex("<(see|seealso)[\\s]+cref=\"([^\">]+)\"[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1)));
        private static readonly IReadOnlyDictionary<string, string> PrimitiveDisplayTypeNameLookups = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [typeof(byte).FullName] = "byte",
            [typeof(sbyte).FullName] = "sbyte",
            [typeof(int).FullName] = "int",
            [typeof(uint).FullName] = "uint",
            [typeof(short).FullName] = "short",
            [typeof(ushort).FullName] = "ushort",
            [typeof(long).FullName] = "long",
            [typeof(ulong).FullName] = "ulong",
            [typeof(float).FullName] = "float",
            [typeof(double).FullName] = "double",
            [typeof(char).FullName] = "char",
            [typeof(bool).FullName] = "bool",
            [typeof(object).FullName] = "object",
            [typeof(string).FullName] = "string",
            [typeof(decimal).FullName] = "decimal",
        };

        public override bool TryPopulateDescription(CompletionItem completion)
        {
            if (completion.IsTagHelperElementCompletion())
            {
                var success = TryPopulateElementDescription(completion);
                return success;
            }

            if (completion.IsTagHelperAttributeCompletion())
            {
                var success = TryPopulateAttributeDescription(completion);
                return success;
            }

            return false;
        }

        private static bool TryPopulateElementDescription(CompletionItem completion)
        {
            var elementDescriptionInfo = completion.GetElementDescriptionInfo();
            if (elementDescriptionInfo.Count == 0)
            {
                return false;
            }

            var descriptionBuilder = new StringBuilder();
            for (var i = 0; i < elementDescriptionInfo.Count; i++)
            {
                var descriptionInfo = elementDescriptionInfo[i];

                if (descriptionBuilder.Length > 0)
                {
                    descriptionBuilder.AppendLine();
                    descriptionBuilder.AppendLine("---");
                }

                descriptionBuilder.Append("**");
                var tagHelperType = descriptionInfo.TagHelperTypeName;
                var reducedTypeName = ReduceTypeName(tagHelperType);
                descriptionBuilder.Append(reducedTypeName);
                descriptionBuilder.AppendLine("**");
                descriptionBuilder.AppendLine();

                var documentation = descriptionInfo.Documentation;
                if (!TryExtractSummary(documentation, out var summaryContent))
                {
                    continue;
                }

                var finalSummaryContent = CleanSummaryContent(summaryContent);
                descriptionBuilder.AppendLine(finalSummaryContent);
            }

            Populate(completion, descriptionBuilder);
            return true;
        }

        private static bool TryPopulateAttributeDescription(CompletionItem completion)
        {
            var attributeDescriptionInfo = completion.GetAttributeDescriptionInfo();
            if (attributeDescriptionInfo.Count == 0)
            {
                return false;
            }

            var descriptionBuilder = new StringBuilder();
            for (var i = 0; i < attributeDescriptionInfo.Count; i++)
            {
                var descriptionInfo = attributeDescriptionInfo[i];

                if (descriptionBuilder.Length > 0)
                {
                    descriptionBuilder.AppendLine();
                    descriptionBuilder.AppendLine("---");
                }

                descriptionBuilder.Append("**");
                var returnTypeName = GetSimpleName(descriptionInfo.ReturnTypeName);
                var reducedReturnTypeName = ReduceTypeName(returnTypeName);
                descriptionBuilder.Append(reducedReturnTypeName);
                descriptionBuilder.Append("** ");
                var tagHelperTypeName = ResolveTagHelperTypeName(descriptionInfo);
                var reducedTagHelperTypeName = ReduceTypeName(tagHelperTypeName);
                descriptionBuilder.Append(reducedTagHelperTypeName);
                descriptionBuilder.Append(".**");
                descriptionBuilder.Append(descriptionInfo.PropertyName);
                descriptionBuilder.AppendLine("**");
                descriptionBuilder.AppendLine();

                var documentation = descriptionInfo.Documentation;
                if (!TryExtractSummary(documentation, out var summaryContent))
                {
                    continue;
                }

                var finalSummaryContent = CleanSummaryContent(summaryContent);
                descriptionBuilder.AppendLine(finalSummaryContent);
            }

            Populate(completion, descriptionBuilder);
            return true;
        }

        private static string CleanSummaryContent(string summaryContent)
        {
            var crefMatches = ExtractCrefRegex.Value.Matches(summaryContent).Reverse();
            var summaryBuilder = new StringBuilder(summaryContent);

            foreach (var cref in crefMatches)
            {
                if (cref.Success)
                {
                    var value = cref.Groups[2].Value;
                    var reducedValue = ReduceCrefValue(value);
                    summaryBuilder.Remove(cref.Index, cref.Length);
                    summaryBuilder.Insert(cref.Index, $"`{reducedValue}`");
                }

            }
            var lines = summaryBuilder.ToString().Split(new[] { '\n' }, StringSplitOptions.None).Select(line => line.Trim());
            var finalSummaryContent = string.Join(Environment.NewLine, lines);
            return finalSummaryContent;
        }

        private static void Populate(CompletionItem completion, StringBuilder documentationBuilder)
        {
            var description = new StringOrMarkupContent(
                new MarkupContent()
                {
                    Kind = MarkupKind.Markdown,
                    Value = documentationBuilder.ToString(),
                });
            completion.Documentation = description;
        }

        private static bool TryExtractSummary(string documentation, out string summary)
        {
            const string summaryStartTag = "<summary>";
            const string summaryEndTag = "</summary>";

            if (string.IsNullOrEmpty(documentation))
            {
                summary = null;
                return false;
            }

            var summaryTagStart = documentation.IndexOf(summaryStartTag, StringComparison.OrdinalIgnoreCase);
            if (summaryTagStart == -1)
            {
                summary = null;
                return false;
            }

            var summaryTagEndStart = documentation.IndexOf(summaryEndTag, StringComparison.OrdinalIgnoreCase);
            if (summaryTagEndStart == -1)
            {
                summary = null;
                return false;
            }

            var summaryContentStart = summaryTagStart + summaryStartTag.Length;
            var summaryContentLength = summaryTagEndStart - summaryContentStart;

            summary = documentation.Substring(summaryContentStart, summaryContentLength);
            return true;
        }

        private static string ReduceCrefValue(string value)
        {
            if (value.Length < 2)
            {
                return string.Empty;
            }

            var type = value[0];
            value = value.Substring(2);

            switch (type)
            {
                case 'T':
                    var reducedCrefType = ReduceTypeName(value);
                    return reducedCrefType;
                case 'P':
                case 'M':
                    var reducedProperty = ReduceTypeName(value);
                    var reducedType = ReduceTypeName(value, value.Length - reducedProperty.Length - 2 /* X. */);
                    var reducedCrefProperty = string.Concat(reducedType, ".", reducedProperty);
                    return reducedCrefProperty;
            }

            return value;
        }

        private static string ReduceTypeName(string content) => ReduceTypeName(content, content.Length - 1);

        private static string ReduceTypeName(string content, int reverseIndexStart)
        {
            int scope = 0;
            for (var i = reverseIndexStart; i >= 0; i--)
            {
                do
                {
                    if (content[i] == '}')
                    {
                        scope++;
                    }
                    else if (content[i] == '{')
                    {
                        scope--;
                    }

                    if (scope > 0)
                    {
                        i--;
                    }
                } while (scope != 0 && i >= 0);

                if (i < 0)
                {
                    // Could not balance scope
                    return content.Substring(0, reverseIndexStart + 1);
                }

                do
                {
                    if (content[i] == ')')
                    {
                        scope++;
                    }
                    else if (content[i] == '{')
                    {
                        scope--;
                    }

                    if (scope > 0)
                    {
                        i--;
                    }
                } while (scope != 0 && i >= 0);

                if (i < 0)
                {
                    // Could not balance scope
                    return content.Substring(0, reverseIndexStart + 1);
                }

                do
                {
                    if (content[i] == '>')
                    {
                        scope++;
                    }
                    else if (content[i] == '<')
                    {
                        scope--;
                    }

                    if (scope > 0)
                    {
                        i--;
                    }
                } while (scope != 0 && i >= 0);

                if (i < 0)
                {
                    // Could not balance scope
                    return content.Substring(0, reverseIndexStart + 1);
                }

                if (content[i] == '.')
                {
                    var piece = content.Substring(i + 1, reverseIndexStart - i);
                    return piece;
                }
            }

            return content.Substring(0, reverseIndexStart + 1);
        }

        private static string GetSimpleName(string typeName)
        {
            if (PrimitiveDisplayTypeNameLookups.TryGetValue(typeName, out var simpleName))
            {
                return simpleName;
            }

            return typeName;
        }

        private static string ResolveTagHelperTypeName(AttributeDescriptionInfo info)
        {
            // A BoundAttributeDescriptor does not have a direct reference to its parent TagHelper.
            // However, when it was constructed the parent TagHelper's type name was embedded into
            // its DisplayName. In VSCode we can't use the DisplayName verbatim for descriptions
            // because the DisplayName is typically too long to display properly. Therefore we need
            // to break it apart and then reconstruct it in a reduced format.

            var simpleReturnType = GetSimpleName(info.ReturnTypeName);
            var typeNamePrefixLength = simpleReturnType.Length + 1 /* space */;
            var typeNameSuffixLength = /* . */ 1 + info.PropertyName.Length;
            var typeNameLength = info.DisplayName.Length - typeNamePrefixLength - typeNameSuffixLength;
            var tagHelperTypeName = info.DisplayName.Substring(typeNamePrefixLength, typeNameLength);
            return tagHelperTypeName;
        }
    }
}
