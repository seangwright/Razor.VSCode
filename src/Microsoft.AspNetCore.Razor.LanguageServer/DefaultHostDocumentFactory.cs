﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.LanguageServer.StrongNamed;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public class DefaultHostDocumentFactory : HostDocumentFactory
    {
        private readonly ILanguageServer _router;

        public DefaultHostDocumentFactory(ILanguageServer router)
        {
            if (router == null)
            {
                throw new ArgumentNullException(nameof(router));
            }

            _router = router;
        }

        public override HostDocumentShim Create(string documentFilePath)
        {
            var hostDocument = HostDocumentShim.Create(documentFilePath, documentFilePath);
            hostDocument.GeneratedCodeContainer.SourceTextContainer.TextChanged += (sender, args) =>
            {
                var textChanges = args.NewText.GetTextChanges(args.OldText);
                var request = new UpdateCSharpBufferRequest()
                {
                    HostDocumentFilePath = documentFilePath,
                    Changes = textChanges,
                };
                OnHostDocumentContainerTextChanged(request);
            };

            return hostDocument;
        }

        private void OnHostDocumentContainerTextChanged(UpdateCSharpBufferRequest request)
        {
            _router.Client.SendRequest("updateCSharpBuffer", request);
        }

        private class UpdateCSharpBufferRequest
        {
            public string HostDocumentFilePath { get; set; }

            public IReadOnlyList<TextChange> Changes { get; set; }
        }
    }
}