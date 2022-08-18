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

        public static void ExtractXPaths(string[] files, string root,
            IDictionary<string, SortedDictionary<string, string>> map)
        {
            foreach (var file in files)
            {
                var shortName = file.Replace(root, string.Empty)[1..];
                Console.WriteLine($" * {shortName}");

                var doc = Load(file);
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

                        if (!map.TryGetValue(onePath, out var exist))
                            map[onePath] = exist = new SortedDictionary<string, string>();
                        exist[aName] = aVal;
                    }
                }
            }
        }

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
    }
}