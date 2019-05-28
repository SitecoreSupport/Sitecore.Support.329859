using Sitecore.Diagnostics;
using Sitecore.Xml.Patch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Sitecore.Xml.Patch
{
    public class XmlPatchHelper
    {
        // Fields
        public const string PatchPrefix = "patch";
        protected IElementIdentification elementIdentification = new ElementIdentification();

        // Methods
        public virtual void AssignAttributes(XmlNode target, IEnumerable<IXmlNode> attributes)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(attributes, "attributes");
            foreach (IXmlNode node in attributes)
            {
                Assert.IsNotNull(target.Attributes, "attributes");
                if (node.LocalName != "xmlns")
                {
                    XmlAttribute attribute = target.Attributes[node.LocalName, node.NamespaceURI];
                    if (attribute == null)
                    {
                        if (this.IsPatchingAttributeName(node.LocalName) && (target.Attributes[node.LocalName] != null))
                        {
                            attribute = target.Attributes[node.LocalName];
                        }
                        else
                        {
                            Assert.IsNotNull(target.OwnerDocument, "document");
                            attribute = target.OwnerDocument.CreateAttribute(this.MakeName(node.Prefix, node.LocalName), node.NamespaceURI);
                            target.Attributes.Append(attribute);
                        }
                    }
                    attribute.Value = node.Value;
                }
            }
        }

        protected virtual void AssignSource(XmlNode target, object source, XmlPatchNamespaces ns)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(source, "source");
            Assert.ArgumentNotNull(ns, "ns");
            IXmlSource source2 = source as IXmlSource;
            if (source2 != null)
            {
                string sourceName = source2.SourceName;
                if (!string.IsNullOrEmpty(sourceName))
                {
                    Assert.IsNotNull(target.OwnerDocument, "target.OwnerDocument");
                    string prefixOfNamespace = target.OwnerDocument.GetPrefixOfNamespace(ns.PatchNamespace);
                    if (string.IsNullOrEmpty(prefixOfNamespace))
                    {
                        prefixOfNamespace = "patch";
                        XmlNode documentElement = target.OwnerDocument.DocumentElement;
                        XmlAttribute node = target.OwnerDocument.CreateAttribute("xmlns:" + prefixOfNamespace);
                        node.Value = ns.PatchNamespace;
                        Assert.IsNotNull(documentElement, "rootElement");
                        Assert.IsNotNull(documentElement.Attributes, "rootElement.Attributes");
                        documentElement.Attributes.Append(node);
                    }
                    Assert.IsNotNull(target.Attributes, "target.Attributes");
                    XmlAttribute attribute = target.Attributes["source", ns.PatchNamespace];
                    if (attribute == null)
                    {
                        attribute = target.OwnerDocument.CreateAttribute(prefixOfNamespace, "source", ns.PatchNamespace);
                        target.Attributes.Append(attribute);
                    }
                    attribute.Value = sourceName;
                }
            }
        }

        protected virtual XmlNamespaceManager BuildNamespaceForNode(IXmlElement node)
        {
            XmlNamespaceManager manager = new XmlNamespaceManager(new NameTable());
            if ((node.Prefix != null) && string.IsNullOrEmpty(manager.LookupPrefix(node.Prefix)))
            {
                if (!string.IsNullOrEmpty(node.Prefix) || !string.IsNullOrEmpty(manager.LookupNamespace("nodens")))
                {
                    manager.AddNamespace(node.Prefix, node.NamespaceURI);
                }
                else
                {
                    manager.AddNamespace("nodens", node.NamespaceURI);
                }
            }
            return manager;
        }

        protected virtual StringBuilder BuildPredicateForNodeAttributes(List<IXmlNode> queryAttributes, XmlNamespaceManager nsManager)
        {
            StringBuilder builder = new StringBuilder();
            bool flag = false;
            foreach (IXmlNode node in queryAttributes)
            {
                if (node.LocalName != "xmlns")
                {
                    if (flag)
                    {
                        builder.Append(" and ");
                    }
                    if ((node.Prefix != null) && string.IsNullOrEmpty(nsManager.LookupPrefix(node.Prefix)))
                    {
                        nsManager.AddNamespace(node.Prefix, node.NamespaceURI);
                    }
                    flag = true;
                    if (node.LocalName == "innerText")
                    {
                        builder.Append($"text() ={ node.Value}");
                    }
                    else
                    {
                        string[] textArray1 = new string[] { "@", this.MakeName(node.Prefix, node.LocalName), "=\"", SecurityElement.Escape(node.Value), "\"" };
                        builder.Append(string.Concat(textArray1));
                    }
                }
            }
            return builder;
        }

        protected virtual float CalculateRelevancy(XmlNode node, IXmlElement patch, int level, XmlPatchNamespaces ns)
        {
            float num = 0f;
            if (level <= 30)
            {
                if (!node.HasChildNodes || !patch.GetChildren().Any<IXmlElement>())
                {
                    return num;
                }
                foreach (IXmlElement element in patch.GetChildren())
                {
                    if (element.NodeType == XmlNodeType.Element)
                    {
                        List<IXmlNode> queryAttributes = this.InitializeQueryAttributes(element, ns);
                        XmlNamespaceManager nsManager = this.BuildNamespaceForNode(element);
                        XmlNode node2 = this.FindBestTargetChild(node, element, this.BuildPredicateForNodeAttributes(queryAttributes, nsManager), ns, nsManager);
                        if (node2 == null)
                        {
                            num--;
                            continue;
                        }
                        num = (num + 1f) + (this.CalculateRelevancy(node2, element, level + 1, ns) / 10f);
                    }
                }
            }
            return num;
        }

        protected virtual bool ContainsPatchNodesOnly(IXmlElement node, XmlPatchNamespaces ns)
        {
            IEnumerable<IXmlElement> source = node.GetChildren().ToList<IXmlElement>();
            return (source.Any<IXmlElement>() && source.All<IXmlElement>(x => (x.NamespaceURI == ns.PatchNamespace)));
        }

        public virtual void CopyAttributes(XmlNode target, IXmlElement patch, XmlPatchNamespaces ns)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(patch, "patch");
            Assert.ArgumentNotNull(ns, "ns");
            IEnumerable<IXmlNode> source = this.GetAttributesToCopy(patch, ns).Select<IXmlNode, IXmlNode>(delegate (IXmlNode a)
            {
                string str = (a.NamespaceURI == ns.SetNamespace) ? string.Empty : a.NamespaceURI;
                XmlNodeInfo info1 = new XmlNodeInfo();
                info1.NodeType = a.NodeType;
                info1.NamespaceURI = str;
                info1.LocalName = a.LocalName;
                info1.Value = a.Value;
                info1.Prefix = a.Prefix;
                return info1;
            });
            if (source.Any<IXmlNode>())
            {
                this.AssignAttributes(target, source);
                this.AssignSource(target, patch, ns);
            }
        }

        protected virtual InsertOperation DetermineInsertOperation(IXmlElement node, XmlPatchNamespaces patchNamespaces)
        {
            InsertOperation operation = null;
            foreach (IXmlNode node2 in node.GetAttributes())
            {
                if (node2.NamespaceURI != patchNamespaces.PatchNamespace)
                {
                    continue;
                }
                string localName = node2.LocalName;
                if (((localName == "b") || ((localName == "before") || ((localName == "a") || ((localName == "after") || (localName == "i"))))) || (localName == "instead"))
                {
                    InsertOperation operation1 = new InsertOperation();
                    operation1.Reference = node2.Value;
                    operation1.Disposition = node2.LocalName[0];
                    operation = operation1;
                }
            }
            return operation;
        }

        protected virtual XmlNode FindBestTargetChild(XmlNode target, IXmlElement patchNode, StringBuilder predicateBuilder, XmlPatchNamespaces ns, XmlNamespaceManager nsManager)
        {
            XmlNode node;
            string xpath = this.MakeName(patchNode.Prefix, patchNode.LocalName);
            if (string.IsNullOrEmpty(patchNode.Prefix) && (nsManager.LookupNamespace("nodens") == patchNode.NamespaceURI))
            {
                xpath = "nodens:" + xpath;
            }
            string str2 = predicateBuilder.ToString();
            if (str2.Length > 0)
            {
                xpath = xpath + "[" + str2 + "]";
            }
            XmlNodeList list = target.SelectNodes(xpath, nsManager);
            if ((list == null) || (list.Count == 0))
            {
                return null;
            }
            if (list.Count == 1)
            {
                return list[0];
            }
            try
            {
                int num = 0;
                float minValue = float.MinValue;
                int num3 = 0;
                while (true)
                {
                    if (num3 >= list.Count)
                    {
                        node = list[num];
                        break;
                    }
                    float num4 = this.CalculateRelevancy(list[num3], patchNode, 1, ns);
                    if (num4 > minValue)
                    {
                        minValue = num4;
                        num = num3;
                    }
                    num3++;
                }
            }
            catch
            {
                node = list[0];
            }
            return node;
        }

        protected virtual IEnumerable<IXmlNode> GetAttributesToCopy(IXmlElement element, XmlPatchNamespaces ns) =>
            (from a in element.GetAttributes()
             where (a.NamespaceURI != ns.PatchNamespace) && (a.NamespaceURI != "http://www.w3.org/2000/xmlns/")
             select a);

        protected virtual List<IXmlNode> InitializeQueryAttributes(IXmlElement node, XmlPatchNamespaces patchNamespaces)
        {
            List<IXmlNode> list = new List<IXmlNode>();
            foreach (IXmlNode node2 in this.elementIdentification.GetSignificantAttributes(node))
            {
                if (node2.Prefix == "xmlns")
                {
                    continue;
                }
                if ((node2.NamespaceURI != patchNamespaces.SetNamespace) && (node2.NamespaceURI != patchNamespaces.PatchNamespace))
                {
                    XmlNodeInfo item = new XmlNodeInfo();
                    item.NodeType = node2.NodeType;
                    item.NamespaceURI = node2.NamespaceURI;
                    item.LocalName = node2.LocalName;
                    item.Prefix = node2.Prefix;
                    item.Value = node2.Value;
                    list.Add(item);
                }
            }
            return list;
        }

        protected virtual List<IXmlNode> InitializeSetAttributes(IXmlElement node, XmlPatchNamespaces patchNamespaces, List<IXmlNode> queryAttributes)
        {
            List<IXmlNode> list = new List<IXmlNode>();
            using (IEnumerator<IXmlNode> enumerator = node.GetAttributes().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    IXmlNode attribute = enumerator.Current;
                    if (attribute.NamespaceURI != patchNamespaces.SetNamespace)
                    {
                        if (queryAttributes.Any<IXmlNode>(a => ReferenceEquals(a, attribute)))
                        {
                            continue;
                        }
                        if (attribute.Prefix == "xmlns")
                        {
                            continue;
                        }
                        if (this.IsSpecificNamespace(attribute, patchNamespaces))
                        {
                            continue;
                        }
                    }
                    XmlNodeInfo info1 = new XmlNodeInfo();
                    info1.NodeType = attribute.NodeType;
                    info1.NamespaceURI = string.Empty;
                    info1.LocalName = attribute.LocalName;
                    info1.Prefix = string.Empty;
                    info1.Value = attribute.Value;
                    XmlNodeInfo item = info1;
                    if (this.IsPatchingAttributeName(attribute.LocalName))
                    {
                        item.Prefix = "patch";
                        item.NamespaceURI = "http://www.sitecore.net/xmlconfig/";
                    }
                    list.Add(item);
                }
            }
            return list;
        }

        protected virtual bool InsertChild(XmlNode parent, XmlNode child, InsertOperation operation)
        {
            Assert.ArgumentNotNull(parent, "parent");
            Assert.ArgumentNotNull(child, "child");
            if (operation == null)
            {
                parent.AppendChild(child);
                return true;
            }
            XmlNode refChild = parent.SelectSingleNode(operation.Reference);
            if (refChild == null)
            {
                parent.AppendChild(child);
                return false;
            }
            char disposition = operation.Disposition;
            if (disposition == 'a')
            {
                parent.InsertAfter(child, refChild);
                return true;
            }
            if (disposition == 'b')
            {
                parent.InsertBefore(child, refChild);
                return true;
            }
            if (disposition != 'i')
            {
                throw new Exception("Insert operation is not implemented");
            }
            if (!ReferenceEquals(child, refChild))
            {
                parent.InsertBefore(child, refChild);
                parent.RemoveChild(refChild);
            }
            else if (child.HasChildNodes)
            {
                child.RemoveAll();
            }
            return true;
        }

        protected virtual XmlNode InsertNode(XmlNode target, IXmlElement node, InsertOperation operation, Stack<InsertOperation> pendingOperations)
        {
            Assert.IsNotNull(target.OwnerDocument, "document");
            XmlElement child = target.OwnerDocument.CreateElement(this.MakeName(node.Prefix, node.LocalName), node.NamespaceURI);
            if (!this.InsertChild(target, child, operation) && (operation != null))
            {
                operation.Node = child;
                pendingOperations.Push(operation);
            }
            return child;
        }

        protected virtual bool IsPatchingAttributeName(string attributeName)
        {
            string str = attributeName.ToLowerInvariant();
            return ((str == "b") || ((str == "before") || ((str == "a") || ((str == "after") || ((str == "i") || (str == "instead"))))));
        }

        protected virtual bool IsSpecificNamespace(IXmlNode node, XmlPatchNamespaces patchNamespaces) =>
            (node.NamespaceURI.Equals(patchNamespaces.SetNamespace, StringComparison.InvariantCultureIgnoreCase) || node.NamespaceURI.Equals(patchNamespaces.PatchNamespace, StringComparison.InvariantCultureIgnoreCase));

        public virtual bool IsXmlPatch(string value)
        {
            Assert.ArgumentNotNull(value, "value");
            return (value.IndexOf("p:p=\"1\"", StringComparison.InvariantCulture) >= 0);
        }

        protected virtual string MakeName(string prefix, string localName)
        {
            Assert.ArgumentNotNull(localName, "localName");
            return (string.IsNullOrEmpty(prefix) ? localName : (prefix + ":" + localName));
        }

        protected virtual void MergeChildren(XmlNode target, IXmlElement patch, XmlPatchNamespaces ns, bool targetWasInserted)
        {
            // Invalid method body.
        }

        public virtual void MergeNodes(XmlNode target, IXmlElement patch, XmlPatchNamespaces ns)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(patch, "patch");
            Assert.ArgumentNotNull(ns, "ns");
            if (this.ShouldPatchNode(target, patch, ns))
            {
                this.CopyAttributes(target, patch, ns);
                this.MergeChildren(target, patch, ns, false);
            }
        }

        protected virtual void ProcessConfigNode(XmlNode target, IXmlElement command, XmlPatchNamespaces patchNamespace)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(command, "command");
            Assert.ArgumentNotNull(patchNamespace, "ns != null");
            Dictionary<string, string> dictionary = this.GetAttributesToCopy(command, patchNamespace).ToDictionary<IXmlNode, string, string>(a => a.LocalName, a => a.Value);
            string localName = command.LocalName;
            if ((localName != "a") && (localName != "attribute"))
            {
                if ((localName == "d") || (localName == "delete"))
                {
                    Assert.IsNotNull(target.ParentNode, "parent");
                    target.ParentNode.RemoveChild(target);
                }
                else if ((localName == "da") || (localName == "deleteattribute"))
                {
                    Assert.IsNotNull(target.Attributes, "attributes");
                    string name = dictionary["name"];
                    target.Attributes.RemoveNamedItem(name);
                }
            }
            else
            {
                string str2;
                XmlAttribute attribute = null;
                string str3;
                dictionary.TryGetValue("ns", out str2);
                Assert.IsNotNull(target.Attributes, "attributes");
                if (target.Attributes[dictionary["name"], str2] == null)
                {
                    Assert.IsNotNull(target.OwnerDocument, "document");
                    attribute = target.OwnerDocument.CreateAttribute(dictionary["name"], str2);
                    target.Attributes.Append(attribute);
                }
                if (!dictionary.TryGetValue("value", out str3))
                {
                    str3 = string.Empty;
                }
                foreach (IXmlElement element in command.GetChildren())
                {
                    string text1 = element.Value;
                    str3 = text1 ?? str3;
                }
                attribute.Value = str3;
            }
        }

        protected virtual bool ShouldInsertNode(XmlNode target, XmlNode patch)
        {
            if (target.ChildNodes.Count == patch.ChildNodes.Count)
            {
                for (int i = 0; i < target.ChildNodes.Count; i++)
                {
                    XmlNode node = target.ChildNodes[i];
                    XmlNode node2 = patch.ChildNodes[i];
                    if (node.HasChildNodes || node2.HasChildNodes)
                    {
                        if (this.ShouldInsertNode(node, node2))
                        {
                            return true;
                        }
                    }
                    else if (node.OuterXml != node2.OuterXml)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        protected virtual bool ShouldPatchNode(XmlNode target, IXmlElement patch, XmlPatchNamespaces ns)
        {
            Assert.ArgumentNotNull(target, "target");
            Assert.ArgumentNotNull(patch, "patch");
            Assert.ArgumentNotNull(ns, "ns");
            return ((target.NamespaceURI == patch.NamespaceURI) && (target.LocalName == patch.LocalName));
        }

        protected virtual bool ShouldProcessPatchNode(IXmlElement patchNode) =>
            (patchNode.NodeType == XmlNodeType.Element);

        protected class InsertOperation
        {
            public char Disposition { get; set; }

            public XmlNode Node { get; set; }

            public string Reference { get; set; }

            public bool Succeeded { get; set; }
        }
    }

}
