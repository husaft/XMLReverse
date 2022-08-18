using System.IO;
using System.Xml.Linq;

namespace XMLReverse.Lib
{
    public static class XmlHelper
    {
        public static XDocument Load(string file)
        {
            using var stream = File.OpenRead(file);
            var doc = XDocument.Load(stream);
            return doc;
        }

        public const string TxtId = "_text_";
        public const string ChildId = "_child_";
    }
}