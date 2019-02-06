// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultTagHelperLookupService : TagHelperLookupService
    {
        private object _lookupLock = new object();
        private Dictionary<string, TagHelperDescriptor> _tagHelpers;

        public DefaultTagHelperLookupService()
        {
            _tagHelpers = new Dictionary<string, TagHelperDescriptor>();
        }

        public override void Initialize(ProjectSnapshotManagerBase projectManager)
        {
            projectManager.Changed += ProjectManager_Changed;

            for (var i = 0; i < projectManager.Projects.Count; i++)
            {
                StoreTagHelpers(projectManager.Projects[i]);
            }
        }

        public override bool TryFindByTypeName(string typeName, out TagHelperDescriptor tagHelper)
        {
            lock (_lookupLock)
            {
                if (_tagHelpers.TryGetValue(typeName, out tagHelper))
                {
                    return true;
                }

                return false;
            }
        }

        private void ProjectManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            lock (_lookupLock)
            {
                if (args.Kind == ProjectChangeKind.ProjectChanged ||
                    args.Kind == ProjectChangeKind.ProjectAdded)
                {
                    StoreTagHelpers(args.Newer);
                }
            }
        }

        private void StoreTagHelpers(ProjectSnapshot projectSnapshot)
        {
            for (var i = 0; i < projectSnapshot.TagHelpers.Count; i++)
            {
                var tagHelper = projectSnapshot.TagHelpers[i];
                var typeName = tagHelper.GetTypeName();
                _tagHelpers[typeName] = tagHelper;
            }
        }
    }
}
