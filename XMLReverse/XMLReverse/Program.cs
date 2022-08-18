using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Xml.Schema;
using XMLReverse.Lib;
using static XMLReverse.Lib.JsonHelper;

namespace XMLReverse
{
    internal static class Program
    {
        private static void Main()
        {
            var app = Env.GetAppFolder();
            var root = Env.Combine(app, "..", "..", "TestData");
            Console.WriteLine(root);

            const SearchOption o = SearchOption.AllDirectories;
            var files = Directory.GetFiles(root, "*.xml", o);

            var paths = ReadFromFile<SortedDictionary<string, SortedDictionary<string, string>>>("paths");
            if (paths.Count == 0)
            {
                XmlHelper.ExtractXPaths(files, root, paths);
                WriteToFile(nameof(paths), paths);
            }


            Console.WriteLine("Done.");
        }
    }
}