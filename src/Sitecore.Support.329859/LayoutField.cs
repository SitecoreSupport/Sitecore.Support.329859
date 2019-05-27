using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Layouts;
using Sitecore.Links;
using Sitecore.Pipelines;
using Sitecore.Pipelines.GetLayoutSourceFields;
using Sitecore.Pipelines.ResolveRenderingDatasource;
using Sitecore.Text;
using Sitecore.Xml;
using Sitecore.Xml.Patch;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Sitecore.Data.Fields
{
    public class LayoutField : CustomField
    {
        public const string EmptyValue = "<r />";
        private readonly XmlDocument data;

        public LayoutField(Field innerField) : base(innerField)
        {
            Assert.ArgumentNotNull(innerField, "innerField");
            this.data = this.LoadData();
        }

        public LayoutField(Item item) : this(item.Fields[FieldIDs.FinalLayoutField])
        {
        }

        public LayoutField(Field innerField, string runtimeValue) : base(innerField, runtimeValue)
        {
            Assert.ArgumentNotNull(innerField, "innerField");
            Assert.ArgumentNotNullOrEmpty(runtimeValue, "runtimeValue");
            this.data = this.LoadData();
        }

        public LayoutField(Item item, string runtimeValue) : this(item.Fields[FieldIDs.FinalLayoutField], runtimeValue)
        {
        }

        public static ID ExtractLayoutID(XmlNode deviceNode)
        {
            Assert.ArgumentNotNull(deviceNode, "deviceNode");
            string attribute = XmlUtil.GetAttribute("l", deviceNode);
            if ((attribute.Length <= 0) || !ID.IsID(attribute))
            {
                return ID.Null;
            }
            return ID.Parse(attribute);
        }

        public static RenderingReference[] ExtractReferences(XmlNode deviceNode, Language language, Database database)
        {
            Assert.ArgumentNotNull(deviceNode, "deviceNode");
            Assert.ArgumentNotNull(language, "language");
            Assert.ArgumentNotNull(database, "database");
            XmlNodeList list = deviceNode.SelectNodes("r");
            Assert.IsNotNull(list, "nodes");
            RenderingReference[] referenceArray = new RenderingReference[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                referenceArray[i] = new RenderingReference(list[i], language, database);
            }
            return referenceArray;
        }

        public XmlNode GetDeviceNode(DeviceItem device) =>
            ((device == null) ? null : this.Data.DocumentElement.SelectSingleNode("d[@id='" + device.ID + "']"));

        public static string GetFieldValue(Field field)
        {
            Assert.ArgumentNotNull(field, "field");
            Assert.IsTrue((field.ID == FieldIDs.LayoutField) || (field.ID == FieldIDs.FinalLayoutField), "The field is not a layout/renderings field");
            GetLayoutSourceFieldsArgs args = new GetLayoutSourceFieldsArgs(field);
            List<string> list = new List<string>();
            if (!GetLayoutSourceFieldsPipeline.Run(args))
            {
                list = (from i in args.FieldValuesSource select i.Value).ToList<string>();
            }
            else
            {
                list.AddRange(args.FieldValuesSource.Select<Field, string>(delegate (Field fieldValue)
                {
                    string text1 = fieldValue.GetValue(false, false);
                    string text3 = text1;
                    if (text1 == null)
                    {
                        string local1 = text1;
                        string inheritedValue = fieldValue.GetInheritedValue(false);
                        text3 = inheritedValue ?? fieldValue.GetValue(false, false, true, false, false);
                    }
                    return text3;
                }));
                list.AddRange(from fieldValue in args.StandardValuesSource select fieldValue.GetStandardValue());
            }
            Stack<string> source = new Stack<string>();
            string str = null;
            foreach (string str2 in list)
            {
                if (!string.IsNullOrWhiteSpace(str2))
                {
                    if (!XmlPatchUtils.IsXmlPatch(str2))
                    {
                        str = str2;
                        break;
                    }
                    source.Push(str2);
                }
            }
            return (!string.IsNullOrWhiteSpace(str) ? source.Aggregate<string, string>(str, new Func<string, string, string>(XmlDeltas.ApplyDelta)) : string.Empty);
        }

        public ID GetLayoutID(DeviceItem device)
        {
            Assert.ArgumentNotNull(device, "device");
            XmlNode deviceNode = this.GetDeviceNode(device);
            return ((deviceNode == null) ? ID.Null : ExtractLayoutID(deviceNode));
        }

        private RenderingParametersFieldCollection GetParametersFields(Item layoutItem, string renderingParameters)
        {
            RenderingParametersFieldCollection fields;
            UrlString parameters = new UrlString(renderingParameters);
            RenderingParametersFieldCollection.TryParse(layoutItem, parameters, out fields);
            return fields;
        }

        public RenderingReference[] GetReferences(DeviceItem device)
        {
            Assert.ArgumentNotNull(device, "device");
            XmlNode deviceNode = this.GetDeviceNode(device);
            return ((deviceNode == null) ? null : ExtractReferences(deviceNode, base.InnerField.Language, base.InnerField.Database));
        }

        protected override string GetValue() =>
            (!base._hasRuntimeValue ? GetFieldValue(base._innerField) : base._runtimeValue);

        private XmlDocument LoadData()
        {
            string str = base.Value;
            return (string.IsNullOrEmpty(str) ? XmlUtil.LoadXml("<r/>") : XmlUtil.LoadXml(str));
        }

        public static implicit operator LayoutField(Field field) =>
            ((field == null) ? null : new LayoutField(field));

        public override void Relink(ItemLink itemLink, Item newLink)
        {
            DeviceDefinition definition2;
            int num3;
            Assert.ArgumentNotNull(itemLink, "itemLink");
            Assert.ArgumentNotNull(newLink, "newLink");
            string str = base.Value;
            if (string.IsNullOrEmpty(str))
            {
                return;
            }
            LayoutDefinition definition = LayoutDefinition.Parse(str);
            ArrayList devices = definition.Devices;
            if (devices == null)
            {
                return;
            }
            string b = itemLink.TargetItemID.ToString();
            string str3 = newLink.ID.ToString();
            int num = devices.Count - 1;
            goto TR_0032;
            TR_0003:
            num--;
            goto TR_0032;
            TR_0006:
            num3--;
            TR_001F:
            while (true)
            {
                if (num3 < 0)
                {
                    break;
                }
                RenderingDefinition definition4 = definition2.Renderings[num3] as RenderingDefinition;
                if (definition4 != null)
                {
                    if (definition4.ItemID == b)
                    {
                        definition4.ItemID = str3;
                    }
                    if (definition4.Datasource == b)
                    {
                        definition4.Datasource = str3;
                    }
                    if (definition4.Datasource == itemLink.TargetPath)
                    {
                        definition4.Datasource = newLink.Paths.FullPath;
                    }
                    if (!string.IsNullOrEmpty(definition4.Parameters))
                    {
                        Item layoutItem = base.InnerField.Database.GetItem(definition4.ItemID);
                        if (layoutItem == null)
                        {
                            goto TR_0006;
                        }
                        else
                        {
                            RenderingParametersFieldCollection parametersFields = this.GetParametersFields(layoutItem, definition4.Parameters);
                            foreach (CustomField field in parametersFields.Values)
                            {
                                if (!string.IsNullOrEmpty(field.Value))
                                {
                                    field.Relink(itemLink, newLink);
                                }
                            }
                            definition4.Parameters = parametersFields.GetParameters().ToString();
                        }
                    }
                    if (definition4.Rules != null)
                    {
                        RulesField field2 = new RulesField(base.InnerField, definition4.Rules.ToString());
                        field2.Relink(itemLink, newLink);
                        definition4.Rules = XElement.Parse(field2.Value);
                    }
                }
                goto TR_0006;
            }
            goto TR_0003;
            TR_0032:
            while (true)
            {
                if (num < 0)
                {
                    base.Value = definition.ToXml();
                    return;
                }
                definition2 = devices[num] as DeviceDefinition;
                if (definition2 == null)
                {
                    goto TR_0003;
                }
                else if (definition2.ID != b)
                {
                    if (definition2.Layout != b)
                    {
                        if (definition2.Placeholders != null)
                        {
                            string targetPath = itemLink.TargetPath;
                            bool flag = false;
                            int num2 = definition2.Placeholders.Count - 1;
                            while (true)
                            {
                                if (num2 >= 0)
                                {
                                    PlaceholderDefinition definition3 = definition2.Placeholders[num2] as PlaceholderDefinition;
                                    if ((definition3 != null) && (string.Equals(definition3.MetaDataItemId, targetPath, StringComparison.InvariantCultureIgnoreCase) || string.Equals(definition3.MetaDataItemId, b, StringComparison.InvariantCultureIgnoreCase)))
                                    {
                                        definition3.MetaDataItemId = newLink.Paths.FullPath;
                                        flag = true;
                                    }
                                    num2--;
                                    continue;
                                }
                                if (!flag)
                                {
                                    break;
                                }
                                goto TR_0003;
                            }
                        }
                        if (definition2.Renderings == null)
                        {
                            goto TR_0003;
                        }
                        else
                        {
                            num3 = definition2.Renderings.Count - 1;
                        }
                    }
                    else
                    {
                        definition2.Layout = str3;
                        goto TR_0003;
                    }
                }
                else
                {
                    definition2.ID = str3;
                    goto TR_0003;
                }
                break;
            }
            goto TR_001F;
        }

        public override void RemoveLink(ItemLink itemLink)
        {
            DeviceDefinition definition2;
            int num3;
            Assert.ArgumentNotNull(itemLink, "itemLink");
            string str = base.Value;
            if (string.IsNullOrEmpty(str))
            {
                return;
            }
            LayoutDefinition definition = LayoutDefinition.Parse(str);
            ArrayList devices = definition.Devices;
            if (devices == null)
            {
                return;
            }
            string b = itemLink.TargetItemID.ToString();
            int num = devices.Count - 1;
            goto TR_0032;
            TR_0003:
            num--;
            goto TR_0032;
            TR_0006:
            num3--;
            TR_001F:
            while (true)
            {
                if (num3 < 0)
                {
                    break;
                }
                RenderingDefinition definition4 = definition2.Renderings[num3] as RenderingDefinition;
                if (definition4 != null)
                {
                    if (definition4.Datasource == itemLink.TargetPath)
                    {
                        definition4.Datasource = string.Empty;
                    }
                    if (definition4.ItemID == b)
                    {
                        definition2.Renderings.Remove(definition4);
                    }
                    if (definition4.Datasource == b)
                    {
                        definition4.Datasource = string.Empty;
                    }
                    if (!string.IsNullOrEmpty(definition4.Parameters))
                    {
                        Item layoutItem = base.InnerField.Database.GetItem(definition4.ItemID);
                        if (layoutItem == null)
                        {
                            goto TR_0006;
                        }
                        else
                        {
                            RenderingParametersFieldCollection parametersFields = this.GetParametersFields(layoutItem, definition4.Parameters);
                            foreach (CustomField field in parametersFields.Values)
                            {
                                if (!string.IsNullOrEmpty(field.Value))
                                {
                                    field.RemoveLink(itemLink);
                                }
                            }
                            definition4.Parameters = parametersFields.GetParameters().ToString();
                        }
                    }
                    if (definition4.Rules != null)
                    {
                        RulesField field2 = new RulesField(base.InnerField, definition4.Rules.ToString());
                        field2.RemoveLink(itemLink);
                        definition4.Rules = XElement.Parse(field2.Value);
                    }
                }
                goto TR_0006;
            }
            goto TR_0003;
            TR_0032:
            while (true)
            {
                if (num < 0)
                {
                    base.Value = definition.ToXml();
                    return;
                }
                definition2 = devices[num] as DeviceDefinition;
                if (definition2 == null)
                {
                    goto TR_0003;
                }
                else if (definition2.ID != b)
                {
                    if (definition2.Layout != b)
                    {
                        if (definition2.Placeholders != null)
                        {
                            string targetPath = itemLink.TargetPath;
                            bool flag = false;
                            int num2 = definition2.Placeholders.Count - 1;
                            while (true)
                            {
                                if (num2 >= 0)
                                {
                                    PlaceholderDefinition definition3 = definition2.Placeholders[num2] as PlaceholderDefinition;
                                    if ((definition3 != null) && (string.Equals(definition3.MetaDataItemId, targetPath, StringComparison.InvariantCultureIgnoreCase) || string.Equals(definition3.MetaDataItemId, b, StringComparison.InvariantCultureIgnoreCase)))
                                    {
                                        definition2.Placeholders.Remove(definition3);
                                        flag = true;
                                    }
                                    num2--;
                                    continue;
                                }
                                if (!flag)
                                {
                                    break;
                                }
                                goto TR_0003;
                            }
                        }
                        if (definition2.Renderings == null)
                        {
                            goto TR_0003;
                        }
                        else
                        {
                            num3 = definition2.Renderings.Count - 1;
                        }
                    }
                    else
                    {
                        definition2.Layout = null;
                        goto TR_0003;
                    }
                }
                else
                {
                    devices.Remove(definition2);
                    goto TR_0003;
                }
                break;
            }
            goto TR_001F;
        }

        public static void SetFieldValue(Field field, string value)
        {
            Field field2;
            Assert.ArgumentNotNull(field, "field");
            Assert.ArgumentNotNull(value, "value");
            Assert.IsTrue((field.ID == FieldIDs.LayoutField) || (field.ID == FieldIDs.FinalLayoutField), "The field is not a layout/renderings field");
            string fieldValue = null;
            bool flag = field.Item.Name == "__Standard Values";
            bool flag2 = field.ID == FieldIDs.LayoutField;
            if (flag & flag2)
            {
                field2 = null;
            }
            else if (flag)
            {
                field2 = field.Item.Fields[FieldIDs.LayoutField];
            }
            else if (!flag2)
            {
                field2 = field.Item.Fields[FieldIDs.LayoutField];
            }
            else
            {
                Field field1;
                TemplateItem template = field.Item.Template;
                if ((template == null) || (template.StandardValues == null))
                {
                    field1 = null;
                }
                else
                {
                    field1 = template.StandardValues.Fields[FieldIDs.FinalLayoutField];
                }
                field2 = field1;
            }
            if (field2 != null)
            {
                fieldValue = GetFieldValue(field2);
            }
            if (XmlUtil.XmlStringsAreEqual(value, fieldValue))
            {
                field.Reset();
            }
            else if (!string.IsNullOrWhiteSpace(fieldValue))
            {
                field.Value = XmlDeltas.GetDelta(value, fieldValue);
            }
            else
            {
                field.Value = value;
            }
        }

        public static void SetFieldValue(Field field, string value, string baseValue)
        {
            Assert.ArgumentNotNull(field, "field");
            Assert.ArgumentNotNull(value, "value");
            Assert.ArgumentNotNull(baseValue, "baseValue");
            Assert.IsTrue((field.ID == FieldIDs.LayoutField) || (field.ID == FieldIDs.FinalLayoutField), "The field is not a layout/renderings field");
            if (XmlUtil.XmlStringsAreEqual(value, baseValue))
            {
                field.Reset();
            }
            else
            {
                string delta = string.IsNullOrWhiteSpace(baseValue) ? value : XmlDeltas.GetDelta(value, baseValue);
                if (!XmlUtil.XmlStringsAreEqual(XmlDeltas.ApplyDelta(baseValue, field.Value), XmlDeltas.ApplyDelta(baseValue, delta)))
                {
                    field.Value = delta;
                }
            }
        }

        internal void SetLayoutHack(string value)
        {
            Assert.ArgumentNotNull(value, "value");
            XmlNodeList list = this.Data.DocumentElement.SelectNodes("d");
            Assert.IsNotNull(list, "nodes");
            if (list.Count > 0)
            {
                foreach (XmlNode node in list)
                {
                    XmlUtil.SetAttribute("l", value, node);
                }
                base.Value = this.Data.OuterXml;
            }
        }

        protected override void SetValue(string value)
        {
            Assert.ArgumentNotNull(value, "value");
            if (base._hasRuntimeValue)
            {
                base._runtimeValue = value;
            }
            SetFieldValue(base._innerField, value);
        }

        public override void ValidateLinks(LinksValidationResult result)
        {
            Assert.ArgumentNotNull(result, "result");
            string str = base.Value;
            if (!string.IsNullOrEmpty(str))
            {
                ArrayList devices = LayoutDefinition.Parse(str).Devices;
                if (devices != null)
                {
                    foreach (DeviceDefinition definition2 in devices)
                    {
                        if (!string.IsNullOrEmpty(definition2.ID))
                        {
                            Item targetItem = base.InnerField.Database.GetItem(definition2.ID);
                            if (targetItem != null)
                            {
                                result.AddValidLink(targetItem, definition2.ID);
                            }
                            else
                            {
                                result.AddBrokenLink(definition2.ID);
                            }
                        }
                        if (!string.IsNullOrEmpty(definition2.Layout))
                        {
                            Item item = base.InnerField.Database.GetItem(definition2.Layout);
                            if (item != null)
                            {
                                result.AddValidLink(item, definition2.Layout);
                            }
                            else
                            {
                                result.AddBrokenLink(definition2.Layout);
                            }
                        }
                        this.ValidatePlaceholderSettings(result, definition2);
                        if (definition2.Renderings != null)
                        {
                            foreach (RenderingDefinition definition3 in definition2.Renderings)
                            {
                                if (definition3.ItemID != null)
                                {
                                    Item item = base.InnerField.Database.GetItem(definition3.ItemID);
                                    if (item != null)
                                    {
                                        result.AddValidLink(item, definition3.ItemID);
                                    }
                                    else
                                    {
                                        result.AddBrokenLink(definition3.ItemID);
                                    }
                                    string datasource = definition3.Datasource;
                                    if (!string.IsNullOrEmpty(datasource))
                                    {
                                        using (new ContextItemSwitcher(base.InnerField.Item))
                                        {
                                            ResolveRenderingDatasourceArgs args = new ResolveRenderingDatasourceArgs(datasource);
                                            CorePipeline.Run("resolveRenderingDatasource", args, false);
                                            datasource = args.Datasource;
                                        }
                                        Item targetItem = base.InnerField.Database.GetItem(datasource);
                                        if (targetItem != null)
                                        {
                                            result.AddValidLink(targetItem, datasource);
                                        }
                                        else if (!datasource.Contains(":"))
                                        {
                                            result.AddBrokenLink(datasource);
                                        }
                                    }
                                    string multiVariateTest = definition3.MultiVariateTest;
                                    if (!string.IsNullOrEmpty(multiVariateTest))
                                    {
                                        Item targetItem = base.InnerField.Database.GetItem(multiVariateTest);
                                        if (targetItem != null)
                                        {
                                            result.AddValidLink(targetItem, multiVariateTest);
                                        }
                                        else
                                        {
                                            result.AddBrokenLink(multiVariateTest);
                                        }
                                    }
                                    string personalizationTest = definition3.PersonalizationTest;
                                    if (!string.IsNullOrEmpty(personalizationTest))
                                    {
                                        Item targetItem = base.InnerField.Database.GetItem(personalizationTest);
                                        if (targetItem != null)
                                        {
                                            result.AddValidLink(targetItem, personalizationTest);
                                        }
                                        else
                                        {
                                            result.AddBrokenLink(personalizationTest);
                                        }
                                    }
                                    if ((item != null) && !string.IsNullOrEmpty(definition3.Parameters))
                                    {
                                        foreach (CustomField field in this.GetParametersFields(item, definition3.Parameters).Values)
                                        {
                                            field.ValidateLinks(result);
                                        }
                                    }
                                    if (definition3.Rules != null)
                                    {
                                        new RulesField(base.InnerField, definition3.Rules.ToString()).ValidateLinks(result);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected virtual void ValidatePlaceholderSettings(LinksValidationResult result, DeviceDefinition device)
        {
            Assert.ArgumentNotNull(result, "result");
            Assert.ArgumentNotNull(device, "device");
            ArrayList placeholders = device.Placeholders;
            if (placeholders != null)
            {
                foreach (PlaceholderDefinition definition in placeholders)
                {
                    if (definition == null)
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(definition.MetaDataItemId))
                    {
                        Item targetItem = base.InnerField.Database.GetItem(definition.MetaDataItemId);
                        if (targetItem != null)
                        {
                            result.AddValidLink(targetItem, definition.MetaDataItemId);
                            continue;
                        }
                        result.AddBrokenLink(definition.MetaDataItemId);
                    }
                }
            }
        }

        // Properties
        public XmlDocument Data =>
            this.data;

    }
}
