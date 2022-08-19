using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
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
            var stats = ReadFromFile<SortedDictionary<string, PathStat>>("stats");
            if (paths.Count == 0)
            {
                stats.Clear();
                XmlHelper.ExtractXPaths(files, root, paths, stats);
                WriteToFile(nameof(paths), paths);
                WriteToFile(nameof(stats), stats);
            }

            const string exampleFile = "example.xml";
            SchemaMaster.CollectAsXml(exampleFile, paths);

            Console.WriteLine("Done.");
        }
    }
}