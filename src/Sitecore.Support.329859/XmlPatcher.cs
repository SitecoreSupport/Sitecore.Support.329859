using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Sitecore.Xml.Patch
{
    public class XmlPatcher
    {
        private XmlPatchNamespaces ns;
        private XmlPatchHelper xmlHelper;

        public XmlPatcher(XmlPatchNamespaces ns, XmlPatchHelper xmlHelper)
        {
            this.ns = ns;
            this.xmlHelper = xmlHelper;
        }

        public XmlPatcher(string setNamespace, string patchNamespace) : this(setNamespace, patchNamespace, new XmlPatchHelper())
        {
        }

        private static XmlPatchNamespaces namespaces1 = new XmlPatchNamespaces();
        public XmlPatcher(string setNamespace, string patchNamespace, XmlPatchHelper xmlHelper) : this(namespaces1, xmlHelper)
        {
            namespaces1.SetNamespace = setNamespace;
            namespaces1.PatchNamespace = patchNamespace;
        }

        public void Merge(XmlNode target, IXmlElement patch)
        {
            this.XmlHelper.MergeNodes(target, patch, this.ns);
        }

        public void Merge(XmlNode target, XmlNode patch)
        {
            this.XmlHelper.MergeNodes(target, new XmlDomSource(patch), this.ns);
        }

        public void Merge(XmlNode target, XmlReader reader)
        {
            this.XmlHelper.MergeNodes(target, new XmlReaderSource(reader), this.ns);
        }

        protected XmlPatchHelper XmlHelper =>
            this.xmlHelper;
    }
}
