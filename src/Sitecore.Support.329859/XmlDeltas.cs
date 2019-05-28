using Sitecore.Data.Fields;
using Sitecore.Diagnostics;
using Sitecore.Xml;
using Sitecore.Xml.Patch;
using System;
using System.Xml;

namespace Sitecore.Support.Data.Fields
{
    public static class XmlDeltas
    {
        public static string ApplyDelta(string baseValue, string delta)
        {
            Assert.ArgumentNotNull(baseValue, "baseValue");
            if (!XmlPatchUtils.IsXmlPatch(delta))
            {
                return baseValue;
            }
            XmlDocument document = XmlUtil.LoadXml(delta);
            Assert.IsNotNull(document, "Layout Delta is not a valid XML");
            System.Xml.XmlNode documentElement = document.DocumentElement;
            Assert.IsNotNull(documentElement, "Xml document root element is missing (delta)");
            XmlDocument document2 = XmlUtil.LoadXml(baseValue);
            Assert.IsNotNull(document2, "Layout Value is not a valid XML");
            System.Xml.XmlNode node2 = document2.DocumentElement;
            Assert.IsNotNull(node2, "Xml document root element is missing (base)");
            new Sitecore.Support.Xml.Patch.XmlPatcher("s", "p").Merge(node2, documentElement);
            return node2.OuterXml;
        }

        public static string GetDelta(string layoutValue, string baseValue)
        {
            XmlDocument original = XmlUtil.LoadXml(baseValue);
            if (original != null)
            {
                XmlDocument modified = XmlUtil.LoadXml(layoutValue);
                if (modified != null)
                {
                    XmlDocument delta = XmlDiffUtils.Compare(original, modified, XmlDiffUtils.GetDefaultElementIdentification(), XmlDiffUtils.GetDefaultPatchNamespaces());
                    if (XmlDiffUtils.IsEmptyDelta(delta))
                    {
                        return string.Empty;
                    }
                    layoutValue = delta.DocumentElement.HasChildNodes ? delta.OuterXml : string.Empty;
                }
            }
            return layoutValue;
        }

        public static string GetFieldValue(Field field, Func<Field, string> getBaseValue)
        {
            Assert.ArgumentNotNull(field, "field");
            Assert.ArgumentNotNull(getBaseValue, "getBaseValue");
            string str = field.GetValue(false, false);
            return (!string.IsNullOrEmpty(str) ? ApplyDelta(getBaseValue(field), str) : field.Value);
        }

        public static string GetStandardValue(Field field) =>
            field.GetStandardValue();

        public static void SetFieldValue(Field field, string value)
        {
            Assert.ArgumentNotNull(field, "field");
            Assert.ArgumentNotNull(value, "value");
            if (field.Item.Name == "__Standard Values")
            {
                field.Value = value;
            }
            else
            {
                field.Value = GetDelta(value, field.GetStandardValue());
            }
        }

        public static Func<Field, string> WithEmptyValue(string emptyValue) =>
            delegate (Field field)
            {
                string standardValue = field.GetStandardValue();
                if ((standardValue == null) || (standardValue.Trim().Length == 0))
                {
                    return emptyValue;
                }
                return standardValue;
            };
    }
}
