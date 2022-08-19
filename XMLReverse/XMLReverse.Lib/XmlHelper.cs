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
        private static readonly Encoding Enc = Encoding.UTF8;

        private static XDocument Load(string file)
        {
            using var stream = File.OpenRead(file);
            var doc = XDocument.Load(stream);
            return doc;
        }

        public const string TxtId = "_text_";
        public const string ChildId = "_child_";
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

            var maxCount = counts.FirstOrDefault().Value[0];
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

        public static XmlSchema LoadSchema(string file)
        {
            using var stream = File.OpenRead(file);
            var doc = XmlSchema.Read(stream, OnValidation);
            return doc;
        }

        private static void OnValidation(object sender, ValidationEventArgs args)
        {
            switch (args.Severity)
            {
                case XmlSeverityType.Warning:
                    Console.Write("WARNING: ");
                    break;
                case XmlSeverityType.Error:
                    Console.Write("ERROR: ");
                    break;
            }
            var row = args.Exception.LineNumber;
            var col = args.Exception.LinePosition;
            Console.Write($" ({row}:{col}) ");
            Console.WriteLine(args.Message);
        }

        internal static string ToXmlPath(string path) => $"{path}.xsd";

        public static void SaveToFile(string path, XmlSchema schema)
        {
            var fileName = ToXmlPath(path);
            using var file = File.Create(fileName);
            using var writer = new XmlTextWriter(file, Enc)
            {
                Formatting = Formatting.Indented
            };
            schema.Write(writer);
        }

        public static XmlReader Check(string path)
        {
            var fileName = ToXmlPath(path);
            using var file = File.Create(fileName);
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                ValidationFlags = XmlSchemaValidationFlags.ProcessInlineSchema |
                                  XmlSchemaValidationFlags.ProcessSchemaLocation |
                                  XmlSchemaValidationFlags.ReportValidationWarnings
            };
            settings.ValidationEventHandler += OnValidation;
            return XmlReader.Create(file, settings);
        }

        public static XmlQualifiedName GetBuiltIn(this XmlTypeCode code)
        {
            var type = XmlSchemaType.GetBuiltInSimpleType(code);
            return type.QualifiedName;
        }

        public static XmlQualifiedName GetBuiltIn(this XmlTypeCode? code) => code?.GetBuiltIn();

        public static XmlTypeCode? EstimateType(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (Convertor.IsBoolean(text, out _))
                return XmlTypeCode.Boolean;
            if (byte.TryParse(text, out _))
                return XmlTypeCode.UnsignedByte;
            if (ushort.TryParse(text, out _))
                return XmlTypeCode.UnsignedShort;
            if (int.TryParse(text, out _))
                return XmlTypeCode.Int;
            if (long.TryParse(text, out _))
                return XmlTypeCode.Long;
            if (float.TryParse(text, out _))
                return XmlTypeCode.Float;
            if (double.TryParse(text, out _))
                return XmlTypeCode.Double;
            if (decimal.TryParse(text, out _))
                return XmlTypeCode.Decimal;
            if (DateTime.TryParse(text, out _))
                return XmlTypeCode.DateTime;
            if (TimeSpan.TryParse(text, out _))
                return XmlTypeCode.Duration;
            if (Convertor.IsUrl(text))
                return XmlTypeCode.AnyUri;
            if (Convertor.IsBase64(text))
                return XmlTypeCode.Base64Binary;
            return XmlTypeCode.String;
        }
    }
}