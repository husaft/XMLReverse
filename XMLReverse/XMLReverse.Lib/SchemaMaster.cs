using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace XMLReverse.Lib
{
    public static class SchemaMaster
    {
        public static void CollectAsXml(string fileName,
            IDictionary<string, SortedDictionary<string, string>> paths)
        {
            var xmlDoc = new XmlDocument();
            foreach (var tuple in paths)
            {
                var currentPath = tuple.Key;
                var currentParts = currentPath.Split('/').Skip(1).ToArray();

                XmlNode currentNode = xmlDoc;
                foreach (var currentPart in currentParts)
                {
                    var currentElem = currentNode[currentPart];
                    if (currentElem == null)
                        currentNode.AppendChild(currentElem = xmlDoc.CreateElement(currentPart));
                    currentNode = currentElem;
                }

                var currentMap = tuple.Value;
                foreach (var mapItem in currentMap)
                {
                    var attrKey = mapItem.Key;
                    var attrVal = mapItem.Value;

                    if (attrKey.Contains(XmlHelper.ChildId))
                    {
                        var childKey = attrKey.Split('}', 2).Last();
                        var simpleNode = currentNode.ChildNodes.OfType<XmlNode>()
                            .FirstOrDefault(t => t.LocalName == childKey);
                        if (simpleNode == null)
                        {
                            var simple = xmlDoc.CreateElement(childKey);
                            simple.AppendChild(xmlDoc.CreateTextNode(attrVal));
                            currentNode.AppendChild(simple);
                            continue;
                        }
                        var simpleTxt = simpleNode.ChildNodes.OfType<XmlText>().Single();
                        simpleTxt.Value = attrVal;
                        continue;
                    }

                    if (attrKey.Equals(XmlHelper.TxtId))
                    {
                        var textNode = currentNode.ChildNodes.OfType<XmlText>().FirstOrDefault();
                        if (textNode == null)
                            currentNode.AppendChild(textNode = xmlDoc.CreateTextNode(attrVal));
                        textNode.Value = attrVal;
                        continue;
                    }

                    var currentAttr = currentNode.Attributes?[attrKey];
                    if (currentAttr == null)
                        currentNode.Attributes!.Append(currentAttr = xmlDoc.CreateAttribute(attrKey));
                    currentAttr.Value = attrVal;
                }
            }

            using var xmlFile = File.Create(fileName);
            using var xml = XmlWriter.Create(xmlFile, new XmlWriterSettings { Indent = true });
            xmlDoc.Save(xml);
        }

        private const string TypeSuffix = "Type";

        public static void GenerateSchema(string file,
            IDictionary<string, SortedDictionary<string, string>> paths, IDictionary<string, PathStat> stats)
        {
            var fileCount = stats.First().Value.NodeFreq.Single().Value;
            var schema = new XmlSchema
            {
                AttributeFormDefault = XmlSchemaForm.Unqualified,
                ElementFormDefault = XmlSchemaForm.Qualified
            };

            foreach (var tuple in paths)
            {
                var currentPath = tuple.Key;
                var currentParts = currentPath.Split('/').Skip(1).ToArray();
                var currentName = currentParts.Last();

                var metaInfo = stats[currentPath];
                var maxAttrCount = metaInfo.AttrFreq.Max(m => m.Value);
                var maxNodeCount = metaInfo.NodeFreq.Max(m => m.Value);

                var meta = tuple.Value;
                var complexName = currentName + TypeSuffix;

                var suffix = 1;
                while (schema.FindByName(complexName) != null)
                    complexName += ++suffix;

                var complexType = new XmlSchemaComplexType { Name = complexName };
                if (meta.TryGetValue(XmlHelper.TxtId, out var txtNode))
                {
                    var simple = new XmlSchemaSimpleContent();
                    var simpleExt = new XmlSchemaSimpleContentExtension();
                    var built = XmlHelper.EstimateType(txtNode);
                    simpleExt.BaseTypeName = built.GetBuiltIn();
                    simple.Content = simpleExt;
                    complexType.ContentModel = simple;
                }
                else
                {
                    var seq = new XmlSchemaSequence();
                    complexType.Particle = seq;
                }

                foreach (var pair in meta)
                {
                    var pk = pair.Key;
                    if (pk.Equals(XmlHelper.TxtId))
                        continue;

                    var guessType = XmlHelper.EstimateType(pair.Value);
                    XmlSchemaObject xso;
                    if (pk.Contains(XmlHelper.ChildId))
                    {
                        var xse = new XmlSchemaElement
                        {
                            SchemaTypeName = guessType.GetBuiltIn(),
                            Name = pk.Split('}', 2).Last()
                        };
                        var (min, max) = GetMinMax(metaInfo, fileCount);
                        if (min != null)
                            xse.MinOccursString = min == -1 ? Unbounded : min.ToString();
                        if (max != null)
                            xse.MaxOccursString = max == -1 ? Unbounded : max.ToString();
                        xso = xse;
                    }
                    else
                    {
                        var xsa = new XmlSchemaAttribute
                        {
                            SchemaTypeName = guessType.GetBuiltIn(),
                            Name = pk,
                            Use = maxAttrCount == metaInfo.AttrFreq[pk]
                                ? XmlSchemaUse.Required
                                : XmlSchemaUse.Optional
                        };
                        xso = xsa;
                    }

                    var cm = complexType.ContentModel;
                    if (cm is XmlSchemaSimpleContent { Content: XmlSchemaSimpleContentExtension xe })
                    {
                        if (xso is XmlSchemaAttribute)
                            xe.Attributes.Add(xso);
                        continue;
                    }

                    if (xso is XmlSchemaAttribute)
                        complexType.Attributes.Add(xso);
                    else if (complexType.Particle is XmlSchemaGroupBase xb)
                        xb.Items.Add(xso);
                }

                if (complexType.Particle is XmlSchemaGroupBase xsb && xsb.Items.Count == 0)
                    complexType.Particle = null;
                schema.Items.Add(complexType);
            }
            schema.Write(file);
        }

        private const string Unbounded = "unbounded";

        private static (int? min, int? max) GetMinMax(PathStat meta, long maxCount)
        {
            int? minOcc = null;
            int? maxOcc = null;
            switch (meta.NodeFreq.Count)
            {
                case 1 when meta.NodeFreq.Single().Value == maxCount:
                    minOcc = 1;
                    maxOcc = 1;
                    break;
                case 1:
                    minOcc = 0;
                    maxOcc = 1;
                    break;
                case > 1 when meta.NodeFreq.First().Value == maxCount:
                    minOcc = 1;
                    maxOcc = -1;
                    break;
                case > 1:
                    minOcc = 0;
                    maxOcc = -1;
                    break;
            }
            return (minOcc, maxOcc);
        }
    }
}