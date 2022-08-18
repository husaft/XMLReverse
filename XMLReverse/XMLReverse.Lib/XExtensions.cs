using System;
using System.Linq;
using System.Xml.Linq;

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
    }
}