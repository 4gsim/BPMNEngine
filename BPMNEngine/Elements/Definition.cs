using System.Text;
using BPMNEngine.Attributes;
using BPMNEngine.Elements.Collaborations;
using BPMNEngine.Interfaces.Elements;

namespace BPMNEngine.Elements
{
    [XMLTagAttribute("bpmn", "definitions")]
    [RequiredAttributeAttribute("id")]
    [ValidParent(null)]
    internal record Definition : AParentElement
    {
        private Dictionary<(string, string), string> cachedXPaths = new();
        
        public Definition(XmlElement elem, XmlPrefixMap map, AElement parent)
            : base(elem, map, parent) { }

        public override Definition OwningDefinition => this;

        public IEnumerable<Diagram> Diagrams => Children.OfType<Diagram>();

        public IEnumerable<MessageFlow> MessageFlows => LocateElementsOfType<MessageFlow>();

        public IElement LocateElement(string id)
            => (this.ID==id
                ? this
                : Children.Traverse(ielem => (ielem is IParentElement element ? element.Children : Array.Empty<IElement>())).FirstOrDefault(elem => elem.ID==id)
            );

        public IEnumerable<T> LocateElementsOfType<T>() where T : IElement
            => Children.Traverse(ielem => (ielem is IParentElement element ? element.Children : Array.Empty<IElement>())).OfType<T>();

        public override bool IsValid(out IEnumerable<string> err)
        {
            var res = base.IsValid(out err);
            if (!Children.Any())
            {
                err = (err?? []).Append("No child elements found in the definition.");
                return false;
            }
            return res;
        }
        
        public string FindXPath(XmlNode node)
        {
            var builder = new StringBuilder();
            XmlNode nodeToCache = null;
            while (node != null)
            {
                switch (node.NodeType)
                {
                    case XmlNodeType.Attribute:
                        builder.Insert(0, "/@" + node.Name);
                        node = ((XmlAttribute)node).OwnerElement;
                        break;
                    case XmlNodeType.Element:
                        if (node.Attributes["id"] == null)
                        {
                            int index = Utility.FindElementIndex(this, (XmlElement)node);
                            builder.Insert(0, "/" + node.Name + "[" + index + "]");
                        }
                        else
                            builder.Insert(0, string.Format("/{0}[@id='{1}']", node.Name, node.Attributes["id"].Value));
                        node = node.ParentNode;
                        if (node?.Attributes?["id"] != null)
                            if (cachedXPaths.TryGetValue((node.Name, node.Attributes["id"].Value), out var cachedXPath))
                                return builder.Insert(0, cachedXPath).ToString();
                            else
                                nodeToCache = node;
                        break;
                    case XmlNodeType.Document:
                        var path = builder.ToString();
                        if (nodeToCache != null)
                            cachedXPaths[(nodeToCache.Name, nodeToCache.Attributes["id"].Value)] =
                                path[..(path.LastIndexOf('/') is var i && i >= 0 ? i : path.Length)]; 
                        return path;
                    default:
                        throw Exception(null, new ArgumentException("Only elements and attributes are supported"));
                }
            }
            throw Exception(null, new ArgumentException("Node was not in a document"));
        }

        internal BusinessProcess OwningProcess { get; set; }

        internal void LogLine(LogLevel level, IElement element, string message, params object[] pars)
            => OwningProcess?.WriteLogLine(element, level, new StackFrame(2, true), DateTime.Now, string.Format(message, pars));
        internal Exception Exception(IElement element, Exception exception)
        {
            OwningProcess?.WriteLogException(element, new StackFrame(2, true), DateTime.Now, exception);
            return exception;
        }
    }
}
