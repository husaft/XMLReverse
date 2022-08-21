using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace XMLReverse.Lib
{
    public static class XmlHelper
    {
        private static XDocument Load(string file)
        {
            using var stream = File.OpenRead(file);
            var doc = XDocument.Load(stream);
            return doc;
        }

        public const string TxtId = "_text_";
        public const string ChildId = "_child_";
        public const string ContainsId = "_node_";
        private const string StartId = "_";

        public static void ExtractXPaths(string[] files, string root,
            IDictionary<string, SortedDictionary<string, string>> map,
            IDictionary<string, PathStat> stats)
        {
            var counts = new SortedDictionary<string, long[]>();
            var attrCounts = new SortedDictionary<(string, string), long[]>();
            foreach (var file in files)
            {
                var shortName = file.Replace(root, string.Empty)[1..];
                Console.WriteLine($" * {shortName}");

                var doc = Load(file);
                foreach (var element in doc.Descendants())
                {
                    var path = element.GetAbsoluteXPath();
                    Console.WriteLine($"    - {path}");

                    if (!counts.TryGetValue(path, out var counter))
                        counts[path] = counter = new[] { 0L };
                    counter[0]++;

                    if (XExtensions.FilterSimpleNodes(new[] { element }).Any())
                        continue;

                    var onePath = Simplify(path);
                    var attrs = element.GetAttributes();

                    foreach (var xAttr in attrs)
                    {
                        var aName = xAttr.Name.ToString().Trim();
                        var aVal = xAttr.Value.Trim();
                        if (string.IsNullOrWhiteSpace(aName) || string.IsNullOrWhiteSpace(aVal))
                            continue;

                        if (!map.TryGetValue(onePath, out var exist))
                            map[onePath] = exist = new SortedDictionary<string, string>();
                        exist[aName] = aVal;

                        var acKey = (onePath, aName);
                        if (!attrCounts.TryGetValue(acKey, out var attrCounter))
                            attrCounts[acKey] = attrCounter = new[] { 0L };
                        attrCounter[0]++;
                    }
                }
            }

            var maxCount = counts.FirstOrDefault().Value?[0] ?? 0;
            stats[StartId] = new PathStat { NodeFreq = { [0] = maxCount } };
            var grp = counts.GroupBy(c => Simplify(c.Key));
            foreach (var pair in grp)
            {
                var subMap = new PathStat();
                foreach (var tuple in pair)
                {
                    var tKey = tuple.Key;
                    var tVal = tuple.Value.Single();
                    var tNum = "1";
                    var fromIdx = tKey.LastIndexOf('[');
                    if (fromIdx != -1)
                    {
                        fromIdx++;
                        var untilIdx = tKey.IndexOf(']', fromIdx);
                        tNum = tKey.Substring(fromIdx, untilIdx - fromIdx);
                    }
                    var numIdx = int.Parse(tNum);
                    subMap.NodeFreq.TryGetValue(numIdx, out var sum);
                    subMap.NodeFreq[numIdx] = sum + tVal;
                }
                stats[pair.Key] = subMap;
            }
            foreach (var pair in attrCounts)
            {
                var pKey = pair.Key;
                var pCurrent = stats[pKey.Item1];
                pCurrent.AttrFreq[pair.Key.Item2] = pair.Value.Single();
            }
        }

        private static string Simplify(string path)
            => Regex.Replace(path, @"\[\d+\]", string.Empty);

        public static XmlQualifiedName GetBuiltIn(this XmlTypeCode code)
        {
            var type = XmlSchemaType.GetBuiltInSimpleType(code);
            return type.QualifiedName;
        }

        public static XmlQualifiedName EstimateType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (Convertor.IsBoolean(text, out var isYesNo))
                return isYesNo
                    ? new XmlQualifiedName("enBool")
                    : XmlTypeCode.Boolean.GetBuiltIn();
            if (byte.TryParse(text, out _))
                return XmlTypeCode.UnsignedByte.GetBuiltIn();
            if (ushort.TryParse(text, out _))
                return XmlTypeCode.UnsignedShort.GetBuiltIn();
            if (int.TryParse(text, out _))
                return XmlTypeCode.Int.GetBuiltIn();
            if (long.TryParse(text, out _))
                return XmlTypeCode.Long.GetBuiltIn();
            if (float.TryParse(text, out _))
                return XmlTypeCode.Float.GetBuiltIn();
            if (double.TryParse(text, out _))
                return XmlTypeCode.Double.GetBuiltIn();
            if (decimal.TryParse(text, out _))
                return XmlTypeCode.Decimal.GetBuiltIn();
            if (DateTime.TryParse(text, out _))
                return XmlTypeCode.String.GetBuiltIn(); // TODO XmlTypeCode.DateTime
            if (TimeSpan.TryParse(text, out _))
                return XmlTypeCode.Duration.GetBuiltIn();
            if (Convertor.IsUrl(text))
                return XmlTypeCode.AnyUri.GetBuiltIn();
            if (Convertor.IsBase64(text))
                return XmlTypeCode.Base64Binary.GetBuiltIn();
            return XmlTypeCode.String.GetBuiltIn();
        }

        public static string GetPrefix(string path, int del)
        {
            var array = path.Split('/');
            var parts = array.Take(array.Length - del).Skip(2);
            var tmp = parts.Select(p => p[..1] + p[^1..]);
            var text = string.Join(string.Empty, tmp);
            return text.ToLowerInvariant();
        }

        public static bool Validate(string xml, string xsd, out IList<string> errors)
        {
            var schemas = new XmlSchemaSet();
            schemas.Add(string.Empty, xsd);
            var doc = XDocument.Load(xml);

            errors = new List<string>();
            try
            {
                var tmp = errors;
                doc.Validate(schemas, (src, evt) =>
                {
                    var sender = GetXElement((XObject)src);
                    var xPath = sender?.GetAbsoluteXPath();
                    var text = $"[{evt.Severity}] {xPath} | {evt.Message}";
                    tmp.Add(text);
                });
            }
            catch (XmlSchemaValidationException xsv)
            {
                const XmlSeverityType severity = XmlSeverityType.Error;
                var src = Path.GetFileName(new Uri(xsv.SourceUri ?? string.Empty).LocalPath);
                var pos = $"({xsv.LineNumber}:{xsv.LinePosition})";
                var line = $"[{severity}] {src} {pos} | {xsv.Message}";
                errors.Add(line);
            }

            return errors.Count == 0;
        }

        private static XElement GetXElement(XObject sender)
        {
            while (sender != null && sender is not XElement)
                sender = sender.Parent;
            return (XElement)sender;
        }
    }
}
