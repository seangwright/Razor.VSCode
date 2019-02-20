// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal static class CompletionItemExtensions
    {
        private const string TagHelperElementDataKey = "_TagHelperElementData_";
        private const string TagHelperAttributeDataKey = "_TagHelperAttributes_";

        public static bool IsTagHelperElementCompletion(this CompletionItem completion)
        {
            if (completion.Data is JObject data && data.ContainsKey(TagHelperElementDataKey))
            {
                return true;
            }

            return false;
        }

        public static bool IsTagHelperAttributeCompletion(this CompletionItem completion)
        {
            if (completion.Data is JObject data && data.ContainsKey(TagHelperAttributeDataKey))
            {
                return true;
            }

            return false;
        }

        public static void SetDescriptionData(this CompletionItem completion, IEnumerable<TagHelperDescriptor> tagHelpers)
        {
            var data = new JObject();
            var elementDescriptions = tagHelpers.Select(tagHelper => new ElementDescriptionInfo(tagHelper.GetTypeName(), tagHelper.Documentation));
            data[TagHelperElementDataKey] = JArray.FromObject(elementDescriptions);
            completion.Data = data;
        }

        public static void SetDescriptionData(this CompletionItem completion, IEnumerable<AttributeDescriptionInfo> attributeDescriptions)
        {
            var data = new JObject();
            data[TagHelperAttributeDataKey] = JArray.FromObject(attributeDescriptions);
            completion.Data = data;
        }

        public static IReadOnlyList<ElementDescriptionInfo> GetElementDescriptionInfo(this CompletionItem completion)
        {
            if (completion.Data is JObject data && data.ContainsKey(TagHelperElementDataKey))
            {
                var descriptionInfo = data[TagHelperElementDataKey] as JArray;
                return descriptionInfo.Select(info => info.ToObject<ElementDescriptionInfo>()).ToList();
            }

            return Array.Empty<ElementDescriptionInfo>();
        }

        public static IReadOnlyList<AttributeDescriptionInfo> GetAttributeDescriptionInfo(this CompletionItem completion)
        {
            if (completion.Data is JObject data && data.ContainsKey(TagHelperAttributeDataKey))
            {
                var descriptionInfo = data[TagHelperAttributeDataKey] as JArray;
                return descriptionInfo.Select(info => info.ToObject<AttributeDescriptionInfo>()).ToList();
            }

            return Array.Empty<AttributeDescriptionInfo>();
        }
    }
}
