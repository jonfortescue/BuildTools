﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using NuGet;
using NuGetPackageVerifier.Logging;

namespace NuGetPackageVerifier.Rules
{
    public class AssemblyHasServicingAttributeRule : IPackageVerifierRule
    {
        public IEnumerable<PackageVerifierIssue> Validate(
            IPackageRepository packageRepo,
            IPackage package,
            IPackageVerifierLogger logger)
        {
            foreach (var currentFile in package.GetFiles())
            {
                var extension = Path.GetExtension(currentFile.Path);
                if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var assemblyPath = Path.ChangeExtension(
                        Path.Combine(Path.GetTempPath(), Path.GetTempFileName()), extension);

                    try
                    {
                        using (var packageFileStream = currentFile.GetStream())
                        using (var fileStream = new FileStream(assemblyPath, FileMode.Create))
                        {
                            packageFileStream.CopyTo(fileStream);
                        }

                        if (AssemblyHelpers.IsAssemblyManaged(assemblyPath))
                        {
                            var assemblyDefinition = AssemblyDefinition.ReadAssembly(assemblyPath);

                            var asmAttrs = assemblyDefinition.CustomAttributes;

                            if (!HasServicingAttribute(asmAttrs))
                            {
                                yield return PackageIssueFactory.AssemblyMissingServicingAttribute(currentFile.Path);
                            }
                        }
                    }
                    finally
                    {
                        if (File.Exists(assemblyPath))
                        {
                            File.Delete(assemblyPath);
                        }
                    }
                }
            }
            yield break;
        }

        private static bool HasServicingAttribute(Mono.Collections.Generic.Collection<CustomAttribute> asmAttrs)
        {
            return asmAttrs.Any(asmAttr => IsValidServicingAttribute(asmAttr));
        }

        private static bool IsValidServicingAttribute(CustomAttribute asmAttr)
        {
            if (asmAttr.AttributeType.FullName != typeof(AssemblyMetadataAttribute).FullName)
            {
                return false;
            }
            if (asmAttr.ConstructorArguments.Count != 2)
            {
                return false;
            }

            var keyValue = asmAttr.ConstructorArguments[0].Value as string;
            var valueValue = asmAttr.ConstructorArguments[1].Value as string;

            return (keyValue == "Serviceable") && (valueValue == "True");
        }
    }
}