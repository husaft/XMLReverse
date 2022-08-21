using System;
using System.Collections.Generic;
using System.IO;
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

            const string schemaFile = "example.xsd";
            SchemaMaster.GenerateSchema(schemaFile, paths, stats);

            const string tableFile = "example.csv";
            CsvTool.GenerateTable(tableFile, paths, stats);

            const string graphFile = "example.dot";
            GraphTool.GenerateGraph(graphFile, paths);

            const string codeFile = "example.cs";
            CodeTool.GenerateCSharp(codeFile, paths);

            if (!XmlHelper.Validate(exampleFile, schemaFile, out var errors))
            {
                Console.Error.WriteLine(string.Join(Environment.NewLine, errors));
                Environment.ExitCode = -errors.Count;
                return;
            }

            Console.WriteLine("Done.");
            Environment.ExitCode = 0;
        }
    }
}