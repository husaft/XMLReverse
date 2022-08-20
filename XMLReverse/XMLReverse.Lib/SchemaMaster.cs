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

            var modelName = $"{Path.GetFileNameWithoutExtension(fileName)}.xsd";
            var model = xmlDoc.CreateProcessingInstruction("xml-model",
                $@"href=""{modelName}""");
            xmlDoc.AppendChild(model);

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
            var schema = new XmlSchema
            {
                AttributeFormDefault = XmlSchemaForm.Unqualified,
                ElementFormDefault = XmlSchemaForm.Qualified
            };

            var skipPaths = new List<string>();
            var allPaths = paths.Keys.Concat(stats.Keys.Skip(1))
                .OrderBy(k => k.Count(l => l == '/'))
                .ThenBy(k => k)
                .Distinct().ToArray();

            foreach (var currentPath in allPaths)
            {
                if (skipPaths.Contains(currentPath))
                    continue;

                var currentParts = currentPath.Split('/').Skip(1).ToArray();
                var currentName = currentParts.Last();

                var metaInfo = stats[currentPath];
                var maxNodeCount = metaInfo.NodeFreq.Sum(m => m.Value);

                if (!paths.TryGetValue(currentPath, out var meta))
                    meta = new SortedDictionary<string, string>();
                Expand(currentPath, meta, allPaths, skipPaths);
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
                    simpleExt.BaseTypeName = built;
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
                    if (pk.Contains(XmlHelper.ContainsId))
                    {
                        var xct = pk.Split('}', 2).Last();
                        var xse = new XmlSchemaElement
                        {
                            Name = xct,
                            SchemaTypeName = new XmlQualifiedName(xct + TypeSuffix)
                        };
                        var childPath = $"{currentPath}/{xse.Name}";
                        var childMeta = stats[childPath];
                        SetMinMax(xse, childMeta, maxNodeCount);

                        if (xse.MinOccurs == 1 &&
                            childMeta.NodeFreq.Count == 1 &&
                            childMeta.AttrFreq.Count == 0)
                        {
                            var tmpCheck = new Dictionary<string, string>();
                            Expand(childPath, tmpCheck, allPaths, skipPaths);
                            if (tmpCheck.Count == 0)
                                xse.MinOccurs = 0;
                        }
                        xso = xse;
                    }
                    else if (pk.Contains(XmlHelper.ChildId))
                    {
                        var xci = pk.Split('}', 2).Last();
                        var xse = new XmlSchemaElement
                        {
                            Name = xci,
                            SchemaTypeName = guessType
                        };
                        var childPath = $"{currentPath}/{xse.Name}";
                        skipPaths.Add(childPath);
                        var childMeta = stats[childPath];
                        SetMinMax(xse, childMeta, maxNodeCount);
                        xso = xse;
                    }
                    else
                    {
                        var xsa = new XmlSchemaAttribute
                        {
                            SchemaTypeName = guessType,
                            Name = pk,
                            Use = maxNodeCount == metaInfo.AttrFreq[pk]
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
                if (schema.Items.Count == 0)
                    schema.Items.Add(CreateRootItem(currentName, complexName));
                schema.Items.Add(complexType);
            }
            AddHelperTypes(schema);
            schema.Write(file);
        }

        private static void AddHelperTypes(XmlSchema schema)
        {
            var boolXml = new XmlSchemaSimpleType { Name = "enBool" };
            var xst = new XmlSchemaSimpleTypeRestriction
            {
                BaseTypeName = XmlTypeCode.Token.GetBuiltIn()
            };
            xst.Facets.Add(new XmlSchemaEnumerationFacet { Value = "yes" });
            xst.Facets.Add(new XmlSchemaEnumerationFacet { Value = "no" });
            boolXml.Content = xst;
            schema.Items.Add(boolXml);
        }

        private static XmlSchemaElement CreateRootItem(string name, string type)
        {
            var xml = new XmlSchemaElement
            {
                Name = name,
                SchemaTypeName = new XmlQualifiedName(type)
            };
            return xml;
        }

        private const string Unbounded = "unbounded";

        private static void SetMinMax(XmlSchemaParticle xml, PathStat meta, long maxCount)
        {
            var (min, max) = GetMinMax(meta, maxCount);
            if (min != null)
                xml.MinOccursString = min == -1 ? Unbounded : min.ToString();
            if (max != null)
                xml.MaxOccursString = max == -1 ? Unbounded : max.ToString();
        }

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

        private static void Expand(string current, IDictionary<string, string> map,
            IEnumerable<string> allPaths, IEnumerable<string> skipPaths)
        {
            const char node = '/';
            var currentParts = current.Split(node);
            var paths = allPaths.Except(skipPaths)
                .Where(p => p.StartsWith(current + node))
                .Select(p => p.Split(node))
                .Where(p => p.Length == currentParts.Length + 1);
            foreach (var path in paths)
            {
                var childName = path.Last();
                var childId = $"{{{XmlHelper.ChildId}}}{childName}";
                if (map.ContainsKey(childId))
                    continue;
                var newId = $"{{{XmlHelper.ContainsId}}}{childName}";
                map.Add(newId, "_");
            }
        }
    }
}
