﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MSBuild.Conversion.Facts;

using Newtonsoft.Json.Linq;

namespace MSBuild.Abstractions
{
    public static class ProjectExtensions
    {
        public static void LogProjectProperties(this IProject project, string logFileName)
        {
            var lines = new List<string>();
            foreach (var prop in project.Properties.OrderBy(p => p.Name))
            {
                lines.Add($"{prop.Name} = {prop.EvaluatedValue}");
            }
            File.WriteAllLines(logFileName, lines);
        }

        public static string GetTargetFramework(this IProject project)
        {
            var tf = project.GetPropertyValue(MSBuildFacts.TargetFrameworkNodeName);
            if (!string.IsNullOrWhiteSpace(tf))
            {
                return tf;
            }

            var tfi = project.GetPropertyValue(MSBuildFacts.LegacyTargetFrameworkPropertyNodeName);
            if (string.IsNullOrWhiteSpace(tfi))
            {
                throw new InvalidOperationException($"{MSBuildFacts.LegacyTargetFrameworkPropertyNodeName} is not set!");
            }

            var tfv = project.GetPropertyValue(MSBuildFacts.LegacyTargetFrameworkVersionNodeName);

            tf = tfi switch
            {
                ".NETFramework" => "net",
                ".NETStandard" => "netstandard",
                ".NETCoreApp" => "netcoreapp",
                ".NETPortable" => "netstandard",
                _ => throw new InvalidOperationException($"Unknown {MSBuildFacts.LegacyTargetFrameworkPropertyNodeName}: {tfi}"),
            };

            if (tfi.Equals(MSBuildFacts.NETPortableTFValuePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var profile = project.GetPropertyValue(MSBuildFacts.LegacyTargetFrameworkProfileNodeName);

                if (string.IsNullOrWhiteSpace(profile) && tfv.Equals(MSBuildFacts.PCLv5value, StringComparison.OrdinalIgnoreCase))
                {
                    tf = GetTargetFrameworkFromProjectJson(project);
                }
                else
                {
                    var netstandardVersion = MSBuildFacts.PCLToNetStandardVersionMapping[profile];
                    tf += netstandardVersion;
                }
            }
            else
            {
                if (tfv == "")
                {
                    throw new InvalidOperationException($"{MSBuildFacts.LegacyTargetFrameworkVersionNodeName} is not set!");
                }

                tf += tfv.TrimStart('v');
            }

            return tf;
        }

        private static string GetTargetFrameworkFromProjectJson(IProject project)
        {
            var projectFolder = project.GetPropertyValue("MSBuildProjectDirectory");
            var projectJsonPath = Path.Combine(projectFolder, "project.json");

            var projectJsonContents = File.ReadAllText(projectJsonPath);

            var json = JObject.Parse(projectJsonContents);

            var frameworks = json["frameworks"];
            var tf = ((JProperty)frameworks.Single()).Name;
            return tf;
        }
    }
}
