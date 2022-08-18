using System;
using System.Collections.Generic;
using System.IO;
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

            var paths = new Dictionary<string, string>();

            foreach (var file in files)
            {
                var shortName = file.Replace(root, string.Empty)[1..];
                Console.WriteLine($" * {shortName}");

                var doc = XmlHelper.Load(file);
                foreach (var element in doc.Descendants())
                {
                    var path = element.GetAbsoluteXPath();
                    Console.WriteLine($"    - {path}");

                    paths[path] = "?";
                }
            }

            JsonHelper.WriteToFile(nameof(paths), paths);

            Console.WriteLine("Done.");
        }
    }
}