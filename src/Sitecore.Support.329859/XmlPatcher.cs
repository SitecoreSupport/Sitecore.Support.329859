using System;
using System.Xml;
using Sitecore.Xml.Patch;

namespace Sitecore.Support.Xml.Patch
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

        public void Merge(System.Xml.XmlNode target, IXmlElement patch)
        {
            this.XmlHelper.MergeNodes(target, patch, this.ns);
        }

        public void Merge(System.Xml.XmlNode target, System.Xml.XmlNode patch)
        {
            this.XmlHelper.MergeNodes(target, new XmlDomSource(patch), this.ns);
        }

        public void Merge(System.Xml.XmlNode target, XmlReader reader)
        {
            this.XmlHelper.MergeNodes(target, new XmlReaderSource(reader), this.ns);
        }

        protected XmlPatchHelper XmlHelper =>
            this.xmlHelper;
    }
}
