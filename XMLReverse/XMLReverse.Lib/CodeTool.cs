using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace XMLReverse.Lib
{
    public static class CodeTool
    {
        public static void GenerateCSharp(string fileName,
            IDictionary<string, SortedDictionary<string, string>> paths)
        {
            using var file = File.CreateText(fileName);
            file.WriteLine("using System;");
            file.WriteLine("using System.Xml.Serialization;");
            file.WriteLine();

            foreach (var path in paths)
                GenerateClass(file, path, paths);

            Env.Execute(fileName);
        }

        private static void GenerateClass(TextWriter file,
            KeyValuePair<string, SortedDictionary<string, string>> path,
            IDictionary<string, SortedDictionary<string, string>> paths)
        {
            var key = path.Key;
            var nsp = GetNameSpace(key);
            file.WriteLine($"namespace {nsp}");
            file.WriteLine("{");
            var className = key.Split('/').Last();
            file.WriteLine($"\t[XmlRoot(\"{className}\")]");
            file.WriteLine($"\tpublic class {className}");
            file.WriteLine("\t{");

            var suffix = new StringWriter();

            foreach (var pair in path.Value
                         .GroupBy(pv => pv.Key.Split('}').Last()))
            {
                var options = pair.Select(x => GetPair(x.Key))
                    .ToArray();
                var n = options.FirstOrDefault(o => o.pPre == XmlHelper.ContainsId);
                var m = options.FirstOrDefault(o => o != n);

                var childKey = $"{key}/{pair.Key}";
                if (!string.IsNullOrWhiteSpace(n.pKey) && !paths.ContainsKey(childKey))
                {
                    var dict = new SortedDictionary<string, string>();
                    SchemaMaster.Expand(childKey, dict, paths.Keys);
                    if (dict.Count == 0)
                        n = (default, default);
                    else
                    {
                        GenerateClass(suffix, new KeyValuePair<string,
                            SortedDictionary<string, string>>(childKey, dict), paths);
                    }
                }

                if (!string.IsNullOrWhiteSpace(n.pKey) && string.IsNullOrWhiteSpace(m.pKey))
                {
                    var pKey = pair.Key;
                    var classToNsp = GraphTool.ToTitleCase(className.ToLower());
                    var pType = $"{classToNsp}.{pair.Key}";
                    var type = "XmlElement";
                    file.WriteLine($"\t\t[{type}(\"{pKey}\")]");
                    file.WriteLine($"\t\tpublic {pType} {pKey} {{ get; set; }}");
                    file.WriteLine();
                    continue;
                }

                if (string.IsNullOrWhiteSpace(n.pKey) && !string.IsNullOrWhiteSpace(m.pKey))
                {
                    var pKey = pair.Key;
                    var pLabel = GraphTool.ToTitleCase(pKey);
                    if (!path.Value.TryGetValue(pKey, out var pValue))
                        pValue = path.Value.First(pv =>
                            pv.Key.EndsWith($"}}{pKey}")).Value;
                    var pType = GetCodeType(XmlHelper.EstimateType(pValue));
                    if (pKey == "_text_")
                    {
                        file.WriteLine("\t\t[XmlText]");
                        pLabel = GraphTool.ToTitleCase(pLabel.Trim('_'));
                    }
                    else
                    {
                        var type = string.IsNullOrWhiteSpace(m.pPre)
                            ? "XmlAttribute"
                            : "XmlElement";
                        file.WriteLine($"\t\t[{type}(\"{pKey}\")]");
                    }
                    file.WriteLine($"\t\tpublic {pType} {pLabel} {{ get; set; }}");
                    file.WriteLine();
                    continue;
                }

                Console.Error.WriteLine($" * Ignoring {childKey} for code gen...");
            }

            file.WriteLine("\t}");
            file.WriteLine("}");
            file.WriteLine();

            var txt = suffix.ToString();
            if (!string.IsNullOrWhiteSpace(txt))
                file.WriteLine(txt);
        }

        private static string GetNameSpace(string key)
        {
            var tmp = key.Split('/').Skip(1)
                .Select(p => GraphTool.ToTitleCase(p.ToLower())).ToArray();
            var nsp = $"Auto.{string.Join(".", tmp.Take(tmp.Length - 1))}".TrimEnd('.');
            return nsp;
        }

        private static (string pPre, string pKey) GetPair(string key)
        {
            var oKey = key.Split('}');
            var pPre = oKey.Length == 2 ? oKey[0].TrimStart('{') : string.Empty;
            var pKey = oKey.Length == 2 ? oKey[1] : oKey[0];
            return (pPre, pKey);
        }

        private static string GetCodeType(XmlQualifiedName name)
        {
            switch (name?.Name)
            {
                case "string": return "string";
                case "float": return "float";
                case "int": return "int";
                case "unsignedShort": return "ushort";
                case "unsignedByte": return "byte";
                case "anyURI": return "Uri";
                case "enBool": return "bool";
                case "base64Binary": return "byte[]";
                default: throw new InvalidOperationException(name?.ToString());
            }
        }
    }
}