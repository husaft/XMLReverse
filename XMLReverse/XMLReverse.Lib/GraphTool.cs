using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static XMLReverse.Lib.XmlHelper;

namespace XMLReverse.Lib
{
    public static class GraphTool
    {
        public static void GenerateGraph(string fileName,
            IDictionary<string, SortedDictionary<string, string>> paths)
        {
            using var file = File.CreateText(fileName);
            file.WriteLine("graph {");

            var classes = new SortedDictionary<string, string>();

            var classNames = paths.Select(ToClassPair).ToArray();
            foreach (var pair in classNames)
                CreateClass(pair, file, classes);
            file.WriteLine();

            var attrNames = paths.Select(ToAttrPair).ToArray();
            foreach (var attr in attrNames)
                CreateAttrs(paths, classes, attr, file);
            file.WriteLine("}");

            file.Flush();
            var imgFile = ConvertDot(fileName);
            Env.Execute(imgFile);
        }

        private static (string f, string p, string k) ToClassPair(KeyValuePair<string,
            SortedDictionary<string, string>> p) => ToClassPair(p.Key);

        private static (string f, string p, string k) ToClassPair(string pKey)
            => (f: pKey, p: GetPrefix(pKey, 1), k: pKey.Split('/').Last());

        private static (string f, Dictionary<string, string[]> a) ToAttrPair(
            KeyValuePair<string, SortedDictionary<string, string>> p)
            => (f: p.Key, a: p.Value.Keys.GroupBy(k => k.Split('}').Last())
                .ToDictionary(c => c.Key,
                    d => d.ToArray()));

        private static void CreateClass((string f, string p, string k) pair,
            TextWriter file, IDictionary<string, string> classes)
        {
            var id = $"C_{$"{pair.p}_{pair.k}".ToLower()}";
            var label = ToTitleCase(pair.k.ToLower());
            WriteNode(file, id, label);
            classes.Add(pair.f, id);
        }

        private static void CreateAttrs(IDictionary<string, SortedDictionary<string, string>> paths,
            IDictionary<string, string> classes, (string f, Dictionary<string, string[]> a) attr,
            TextWriter file)
        {
            var nodeId = classes[attr.f];
            foreach (var match in attr.a)
            {
                var key = match.Key;
                var array = match.Value;
                if (array.Any(a => a.Contains(ContainsId)))
                {
                    var subKey = $"{attr.f}/{key}";
                    if (classes.TryGetValue(subKey, out var exist))
                    {
                        WriteEdge(file, nodeId, exist);
                        file.WriteLine();
                        continue;
                    }
                    if (classes.Any(c => c.Key.StartsWith($"{subKey}/")))
                    {
                        CreateClass(ToClassPair(subKey), file, classes);
                        WriteEdge(file, nodeId, classes[subKey]);

                        var dict = new SortedDictionary<string, string>();
                        SchemaMaster.Expand(subKey, dict, paths.Keys);
                        var n = (f: subKey, a: dict.ToDictionary(k =>
                                k.Key.Split('}').Last(),
                            v => new[] { v.Key }));
                        CreateAttrs(paths, classes, n, file);

                        file.WriteLine();
                        continue;
                    }
                    array = new[] { key };
                }
                if (array.Length == 1)
                {
                    var id = $"A{nodeId[1..]}_{key}";
                    var label = match.Key.TrimEnd('_');
                    if (label[0] == '_') label = $"#{label[1..]}";
                    WriteAttr(file, id, label);
                    WriteEdge(file, nodeId, id);
                    file.WriteLine();
                    continue;
                }
                throw new InvalidOperationException(attr.f);
            }
        }

        private static string ConvertDot(string file, string exe = "fdp")
        {
            var outFile = $"{Path.GetFileNameWithoutExtension(file)}.png";
            var args = $@"-Tpng -o{outFile} ""{file}""";
            var workDir = Path.GetDirectoryName(file) ?? string.Empty;
            var info = new ProcessStartInfo(exe, args)
            {
                WorkingDirectory = workDir
            };
            Process.Start(info)!.WaitForExit();
            outFile = Path.Combine(workDir, outFile);
            return outFile;
        }

        private static void WriteNode(TextWriter file, string id, string label)
        {
            const string shapeName = "record";
            file.WriteLine($"\t{id} [shape={shapeName},fillcolor=yellow,style=filled,label=\"{label}\"]");
        }

        private static void WriteEdge(TextWriter file, string first, string second)
        {
            file.WriteLine($"\t{first} -- {second}");
        }

        private static void WriteAttr(TextWriter file, string id, string label)
        {
            const string shapeName = "oval";
            var color = label.All(char.IsUpper) ? "lightgreen" : "green";
            file.WriteLine($"\t{id} [shape={shapeName},fillcolor={color},style=filled,label=\"{label}\"]");
        }

        internal static string ToTitleCase(string text)
        {
            return text[..1].ToUpperInvariant() + text[1..];
        }
    }
}