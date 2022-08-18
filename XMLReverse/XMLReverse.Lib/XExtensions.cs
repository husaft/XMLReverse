using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace XMLReverse.Lib
{
    public static class XExtensions
    {
        public static string GetAbsoluteXPath(this XElement elem)
        {
            if (elem == null)
                throw new ArgumentNullException(nameof(elem));
            var anc = from e in elem.Ancestors() select RelativeXPath(e);
            return $"{string.Concat(anc.Reverse().ToArray())}{RelativeXPath(elem)}";
        }

        private static string RelativeXPath(XElement e)
        {
            var index = e.IndexPosition();
            var current = e.Name.Namespace;
            var name = string.IsNullOrEmpty(current.ToString())
                ? e.Name.LocalName
                : $"*[local-name()='{e.Name.LocalName}']";
            return index is -1 or -2 ? $"/{name}" : $"/{name}[{index}]";
        }

        private static int IndexPosition(this XElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            if (element.Parent == null)
                return -1;
            if (element.Parent.Elements(element.Name).Count() == 1)
                return -2;
            var i = 1;
            foreach (var sibling in element.Parent.Elements(element.Name))
            {
                if (sibling == element)
                    return i;
                i++;
            }
            throw new InvalidOperationException("Element has been removed from its parent");
        }

        public static IEnumerable<XAttribute> GetAttributes(this XElement element)
        {
            var attrs = element.Attributes();
            var text = GetText(element);
            if (!string.IsNullOrWhiteSpace(text))
                attrs = attrs.Concat(new[] { new XAttribute(XmlHelper.TxtId, text) });
            var children = FilterSimpleNodes(element.Nodes())
                .Select(t =>
                {
                    var local = t.node.Name.LocalName;
                    var np = XName.Get(local, XmlHelper.ChildId);
                    return new XAttribute(np, t.txt);
                })
                .ToArray();
            if (children.Any())
                attrs = attrs.Concat(children);
            return attrs;
        }

        public static IEnumerable<(XElement node, string txt)> FilterSimpleNodes(IEnumerable<XNode> nodes)
            => nodes.OfType<XElement>()
                .Where(t => !t.HasAttributes)
                .Select(t => (node: t, txt: GetText(t)))
                .Where(t => !string.IsNullOrWhiteSpace(t.txt));

        private static string GetText(XContainer e)
        {
            var texts = e.Nodes().OfType<XText>().Select(t => t.Value);
            return string.Join(" ", texts).Trim();
        }

        public static IList<string> Verify(this XDocument doc, XmlSchema schema)
        {
            var schemas = new XmlSchemaSet();
            schemas.Add(schema);
            var bld = new List<string>();
            doc.Validate(schemas, (_, e) => bld.Add(e.Message));
            return bld;
        }

        public static IList<string> Verify(this Stream stream, XmlSchema schema)
        {
            var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
            settings.Schemas.Add(schema);
            var document = new XmlDocument();
            using var reader = XmlReader.Create(stream, settings);
            document.Load(reader);
            var bld = new List<string>();
            document.Validate((_, e) => bld.Add(e.Message));
            return bld;
        }

        public static void Write(this XmlSchema schema, string file, out IList<string> list)
        {
            using var stream = File.Create(XmlHelper.ToXmlPath(file));
            list = new List<string>();
            var lists = new[] { list };

            var set = new XmlSchemaSet();
            set.ValidationEventHandler += (_, e) => lists[0].Add(e.Message);
            set.Add(schema);
            set.Compile();

            XmlSchema mySchema = null;
            foreach (XmlSchema item in set.Schemas())
                mySchema = item;

            var table = new NameTable();
            var manager = new XmlNamespaceManager(table);
            manager.AddNamespace("xs", XsSchema);
            mySchema!.Write(stream, manager);
        }

        private const string XsSchema = "http://www.w3.org/2001/XMLSchema";
    }
}