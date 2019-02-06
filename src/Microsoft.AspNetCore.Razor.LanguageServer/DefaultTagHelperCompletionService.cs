// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.VisualStudio.Editor.Razor;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using RazorTagHelperCompletionService = Microsoft.VisualStudio.Editor.Razor.TagHelperCompletionService;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultTagHelperCompletionService : TagHelperCompletionService
    {
        private static readonly Lazy<Regex> ExtractCrefRegex = new Lazy<Regex>(
            () => new Regex("<(see|seealso)[\\s]+cref=\"([^\">]+)\"[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1)));
        private const string AssociatedTagHelpersKey = "_TagHelpers_";
        private static readonly HashSet<string> HtmlSchemaTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DOCTYPE",
            "a",
            "abbr",
            "acronym",
            "address",
            "applet",
            "area",
            "article",
            "aside",
            "audio",
            "b",
            "base",
            "basefont",
            "bdi",
            "bdo",
            "big",
            "blockquote",
            "body",
            "br",
            "button",
            "canvas",
            "caption",
            "center",
            "cite",
            "code",
            "col",
            "colgroup",
            "data",
            "datalist",
            "dd",
            "del",
            "details",
            "dfn",
            "dialog",
            "dir",
            "div",
            "dl",
            "dt",
            "em",
            "embed",
            "fieldset",
            "figcaption",
            "figure",
            "font",
            "footer",
            "form",
            "frame",
            "frameset",
            "h1",
            "h2",
            "h3",
            "h4",
            "h5",
            "h6",
            "head",
            "header",
            "hr",
            "html",
            "i",
            "iframe",
            "img",
            "input",
            "ins",
            "kbd",
            "label",
            "legend",
            "li",
            "link",
            "main",
            "map",
            "mark",
            "meta",
            "meter",
            "nav",
            "noframes",
            "noscript",
            "object",
            "ol",
            "optgroup",
            "option",
            "output",
            "p",
            "param",
            "picture",
            "pre",
            "progress",
            "q",
            "rp",
            "rt",
            "ruby",
            "s",
            "samp",
            "script",
            "section",
            "select",
            "small",
            "source",
            "span",
            "strike",
            "strong",
            "style",
            "sub",
            "summary",
            "sup",
            "svg",
            "table",
            "tbody",
            "td",
            "template",
            "textarea",
            "tfoot",
            "th",
            "thead",
            "time",
            "title",
            "tr",
            "track",
            "tt",
            "u",
            "ul",
            "var",
            "video",
            "wbr",
        };
        private readonly RazorTagHelperCompletionService _razorTagHelperCompletionService;
        private readonly TagHelperLookupService _tagHelperLookupService;

        public DefaultTagHelperCompletionService(
            RazorTagHelperCompletionService razorCompletionService,
            TagHelperLookupService tagHelperLookupService)
        {
            if (razorCompletionService == null)
            {
                throw new ArgumentNullException(nameof(razorCompletionService));
            }

            if (tagHelperLookupService == null)
            {
                throw new ArgumentNullException(nameof(tagHelperLookupService));
            }

            _razorTagHelperCompletionService = razorCompletionService;
            _tagHelperLookupService = tagHelperLookupService;
        }

        public override IReadOnlyList<CompletionItem> GetCompletionsAt(SourceSpan location, RazorCodeDocument codeDocument)
        {
            if (codeDocument == null)
            {
                throw new ArgumentNullException(nameof(codeDocument));
            }

            var syntaxTree = codeDocument.GetSyntaxTree();
            var change = new SourceChange(location, "");
            var owner = syntaxTree.Root.LocateOwner(change);
            var parent = owner.Parent;

            if (parent is MarkupStartTagSyntax startTag)
            {
                // Performing completion on an Html start tag name

                var containingTagName = startTag.Name.Content;
                var attributes = StringifyAttributes(startTag.Attributes);
                var ancestors = parent.Ancestors();
                var tagHelperDocumentContext = codeDocument.GetTagHelperContext();
                var (ancestorTagName, ancestorIsTagHelper) = GetNearestAncestorTagInfo(ancestors);
                var elementCompletionContext = new ElementCompletionContext(
                    tagHelperDocumentContext,
                    existingCompletions: Enumerable.Empty<string>(),
                    containingTagName,
                    attributes,
                    ancestorTagName,
                    ancestorIsTagHelper,
                    HtmlSchemaTagNames.Contains);

                var completionItems = new List<CompletionItem>();
                var completionResult = _razorTagHelperCompletionService.GetElementCompletions(elementCompletionContext);
                foreach (var completion in completionResult.Completions)
                {
                    var data = new JObject();
                    var tagHelperTypeNames = completion.Value.Select(tagHelper => tagHelper.GetTypeName()).ToArray();
                    data[AssociatedTagHelpersKey] = new JArray(tagHelperTypeNames);
                    var razorCompletionItem = new CompletionItem()
                    {
                        Label = completion.Key,
                        InsertText = completion.Key,
                        FilterText = completion.Key,
                        SortText = completion.Key,
                        Kind = CompletionItemKind.TypeParameter,
                        Data = data,
                    };

                    completionItems.Add(razorCompletionItem);
                }

                return completionItems;
            }
            else if (parent is MarkupTagHelperStartTagSyntax startTagHelper)
            {
                // Performing completion on a TagHelper start tag name

                var containingTagName = startTagHelper.Name.Content;
                var attributes = StringifyAttributes(startTagHelper.Attributes);
                var ancestors = parent.Ancestors();
                var tagHelperDocumentContext = codeDocument.GetTagHelperContext();
                var (ancestorTagName, ancestorIsTagHelper) = GetNearestAncestorTagInfo(ancestors);
            }
            else
            {
                // Invalid location for TagHelper completions.
                return Array.Empty<CompletionItem>();
            }

            var parentKind = parent.Kind;
            if (parentKind == SyntaxKind.MarkupStartTag || parentKind == SyntaxKind.MarkupTagHelperStartTag)
            {

            }

            return Array.Empty<CompletionItem>();
        }

        public override bool TryGetDocumentation(CompletionItem completionItem, out StringOrMarkupContent body)
        {
            const string summaryStartTag = "<summary>";
            const string summaryEndTag = "</summary>";

            if (completionItem.Data is JObject data && data.TryGetValue(AssociatedTagHelpersKey, out var entry))
            {
                var associatedTagHelperTypes = entry as JArray;

                if (associatedTagHelperTypes.Count == 0)
                {
                    body = null;
                    return false;
                }

                var documentationBuilder = new StringBuilder();
                for (var i = 0; i < associatedTagHelperTypes.Count; i++)
                {
                    var tagHelperType = associatedTagHelperTypes[i].ToString();

                    if (documentationBuilder.Length > 0)
                    {
                        documentationBuilder.AppendLine();
                        documentationBuilder.AppendLine("---");
                    }

                    documentationBuilder.Append("**");
                    var lastSeparator = tagHelperType.LastIndexOf('.');
                    var reducedTypeName = ExtractDocumentationTypePiece(tagHelperType, tagHelperType.Length - 1);
                    documentationBuilder.Append(reducedTypeName);
                    documentationBuilder.AppendLine("**");
                    documentationBuilder.AppendLine();


                    if (_tagHelperLookupService.TryFindByTypeName(tagHelperType, out var tagHelper) &&
                        tagHelper.Documentation != null)
                    {
                        var summaryTagStart = tagHelper.Documentation.IndexOf(summaryStartTag, StringComparison.OrdinalIgnoreCase);
                        if (summaryTagStart == -1)
                        {
                            body = null;
                            return false;
                        }

                        var summaryTagEndStart = tagHelper.Documentation.IndexOf(summaryEndTag, StringComparison.OrdinalIgnoreCase);
                        if (summaryTagEndStart == -1)
                        {
                            body = null;
                            return false;
                        }

                        var summaryContentStart = summaryTagStart + summaryStartTag.Length;
                        var summaryContentLength = summaryTagEndStart - summaryContentStart;
                        var summaryContent = tagHelper.Documentation.Substring(summaryContentStart, summaryContentLength);
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
                        documentationBuilder.AppendLine(finalSummaryContent);
                    }
                }

                body = new StringOrMarkupContent(
                    new MarkupContent()
                    {
                        Kind = MarkupKind.Markdown,
                        Value = documentationBuilder.ToString(),
                    });
                return true;
            }

            body = null;
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
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
                    var reducedCrefType = ExtractDocumentationTypePiece(value, value.Length - 1);
                    return reducedCrefType;
                case 'P':
                case 'M':
                    var reducedProperty = ExtractDocumentationTypePiece(value, value.Length - 1);
                    var reducedType = ExtractDocumentationTypePiece(value, value.Length - reducedProperty.Length - 2 /* X. */);
                    var reducedCrefProperty = string.Concat(reducedType, ".", reducedProperty);
                    return reducedCrefProperty;
            }

            return value;
        }

        private static string ExtractDocumentationTypePiece(string content, int reverseIndexStart)
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
                    return content.Substring(0, reverseIndexStart);
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
                    return content.Substring(0, reverseIndexStart);
                }

                if (content[i] == '.')
                {
                    var piece = content.Substring(i + 1, reverseIndexStart - i);
                    return piece;
                }
            }

            return content.Substring(0, reverseIndexStart);
        }

        private static IEnumerable<KeyValuePair<string, string>> StringifyAttributes(SyntaxList<RazorSyntaxNode> attributes)
        {
            var stringifiedAttributes = new List<KeyValuePair<string, string>>();

            // TODO FIX ATTRIBUTES

            return stringifiedAttributes;
        }

        private static (string ancestorTagName, bool ancestorIsTagHelper) GetNearestAncestorTagInfo(IEnumerable<SyntaxNode> ancestors)
        {
            foreach (var ancestor in ancestors)
            {
                if (ancestor is MarkupStartTagSyntax startTag)
                {
                    return (startTag.Name.Content, ancestorIsTagHelper: false);
                }
                else if (ancestor is MarkupTagHelperStartTagSyntax startTagHelper)
                {
                    return (startTagHelper.Name.Content, ancestorIsTagHelper: true);
                }
            }

            return (ancestorTagName: null, ancestorIsTagHelper: false);
        }
    }
}
