using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XMLReverse.Lib;

namespace XMLReverse
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var app = Env.GetAppFolder();
            var root = Env.Combine(app, "..", "..", "TestData");
            Console.WriteLine(root);

            const SearchOption o = SearchOption.AllDirectories;
            var files = Directory.GetFiles(root, "*.xml", o);

            var paths = new SortedDictionary<string, IDictionary<string, string>>();

            foreach (var file in files)
            {
                var shortName = file.Replace(root, string.Empty)[1..];
                Console.WriteLine($" * {shortName}");

                var doc = XmlHelper.Load(file);
                foreach (var element in doc.Descendants())
                {
                    var path = element.GetAbsoluteXPath();
                    Console.WriteLine($"    - {path}");

                    if (XExtensions.FilterSimpleNodes(new[] { element }).Any())
                        continue;

                    var onePath = Regex.Replace(path, @"\[\d+\]", string.Empty);
                    var attrs = element.GetAttributes();

                    foreach (var xAttr in attrs)
                    {
                        var aName = xAttr.Name.ToString().Trim();
                        var aVal = xAttr.Value.Trim();
                        if (string.IsNullOrWhiteSpace(aName) || string.IsNullOrWhiteSpace(aVal))
                            continue;
                        if (!paths.TryGetValue(onePath, out var exist))
                            paths[onePath] = exist = new SortedDictionary<string, string>();
                        exist[aName] = aVal;
                    }
                }
            }

            JsonHelper.WriteToFile(nameof(paths), paths);

            Console.WriteLine("Done.");
        }
    }
}