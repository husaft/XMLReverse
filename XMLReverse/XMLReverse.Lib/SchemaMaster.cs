using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

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
    }
}