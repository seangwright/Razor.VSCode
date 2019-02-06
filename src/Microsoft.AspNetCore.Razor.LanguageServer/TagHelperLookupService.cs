// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class TagHelperLookupService : ProjectSnapshotChangeTrigger
    {
        public abstract bool TryFindByTypeName(string typeName, out TagHelperDescriptor tagHelper);
    }
}
