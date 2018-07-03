﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PubComp.Building.NuGetPack
{
    public class NuspecCreatorNetStandard:NuspecCreatorBase
    {
        public NuspecCreatorNetStandard()
        {
            TargetFrameworkElement = "TargetFramework";
        }

        public override List<DependencyInfo> GetDependencies(
            string projectPath, out XAttribute dependenciesAttribute)
        {
            var version = GetFrameworkVersion(projectPath);
            var targetFramework = version !=null ? $".{version}".Replace("netstandard","NETStandard") : ".NETStandard2.0";

            dependenciesAttribute = new XAttribute("targetFramework", targetFramework);

            var result = new List<DependencyInfo>();
            result.AddRange(GetPackageDependenciesNetStandard(projectPath));

            return result;
        }


        protected override string GetOutputPath(XDocument csProj, bool isDebug, string projectFolder)
        {
            var xmlns = csProj.Root.GetDefaultNamespace();
            var proj = csProj.Element(xmlns + "Project");
            var propGroups = proj.Elements(xmlns + "PropertyGroup").ToList();

            var config = (isDebug ? "Debug|AnyCPU" : "Release|AnyCPU");

            var outputPathElement = propGroups
                .Where(el => el.Attribute("Condition") != null
                             && el.Attribute("Condition").Value.Contains(config))
                .Elements(xmlns + "OutputPath").FirstOrDefault();

            string outputPath;
            if (outputPathElement == null)
            {
                var targetFramework = propGroups.FirstOrDefault(pg => pg.Elements("TargetFramework").Any())
                    .Element("TargetFramework");
                outputPath = Path.Combine(projectFolder, $"bin\\{(isDebug ? "debug" : "release")}\\{targetFramework.Value}");
            }
            else
            {
                outputPath = Path.Combine(projectFolder, outputPathElement.Value);
            }

            return outputPath;
        }

        protected override bool DoesProjectContainFile(string projectPath,string file,IEnumerable<XElement> noneElements,IEnumerable<XElement> codeElements)
        {
            var directoryContainsFile = !Directory.GetFiles(Path.GetDirectoryName(projectPath)).Contains(file);
            var projIgnoresFile = noneElements.Any(e =>
                string.Equals(e.Attribute("Remove")?.Value, file, StringComparison.CurrentCultureIgnoreCase));

            var projIncludesFile = codeElements.Any(e =>
                string.Equals(e.Attribute("Include")?.Value, file, StringComparison.CurrentCultureIgnoreCase));

            return (directoryContainsFile && !projIgnoresFile) || projIncludesFile;
        }

        protected override string GetContentFileTarget(XElement el,XNamespace xmlns)
        {
            return el.Attribute("Link")?.Value;
        }

        protected override string FormatFrameworkVersion(string targetFrameworkVersion)
        {
            return targetFrameworkVersion;
        }

        private List<DependencyInfo> GetPackageDependenciesNetStandard(string projectPath)
        {
            var result = new List<DependencyInfo>();

            if (!File.Exists(projectPath))
                return result;

            var project = XDocument.Load(projectPath);

            var packages = project.XPathSelectElements("//ItemGroup//PackageReference").ToList();

            foreach (var package in packages)
            {
                // ReSharper disable PossibleNullReferenceException
                result.Add(
                    new DependencyInfo(
                        ElementType.NuGetDependency,
                        new XElement("dependency",
                            new XAttribute("id", package.Attribute("Include").Value),
                            new XAttribute("version", package.Attribute("Version").Value))));
                // ReSharper restore PossibleNullReferenceException
            }

            return result;

        }
    }
}
