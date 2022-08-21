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

        public static void Write(this XmlSchema schema, string file, bool validate = true)
        {
            var list = new List<string>();
            var lists = new[] { list };

            var set = new XmlSchemaSet();
            set.ValidationEventHandler += (_, e) => lists[0].Add(e.Message);
            set.Add(schema);
            set.Compile();

            if (list.Count != 0 && validate)
                throw new InvalidOperationException(string.Join(Environment.NewLine, list));

            var mySchema = schema;
            foreach (XmlSchema item in set.Schemas())
                mySchema = item;

            var table = new NameTable();
            var manager = new XmlNamespaceManager(table);
            manager.AddNamespace("xs", XsSchema);

            using var stream = File.Create(file);
            using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Indent = true });
            mySchema!.Write(writer, manager);
        }

        private const string XsSchema = "http://www.w3.org/2001/XMLSchema";

        public static XmlSchemaObject FindByName(this XmlSchema schema, string name)
        {
            foreach (var item in schema.Items)
                if (item is XmlSchemaElement xse && name.Equals(xse.Name))
                    return item;
                else if (item is XmlSchemaType xst && name.Equals(xst.Name))
                    return item;
            return null;
        }
    }
}