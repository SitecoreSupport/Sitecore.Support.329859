using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Globalization;
using Sitecore.IO;
using Sitecore.Layouts;
using Sitecore.Resources.Media;
using Sitecore.Sites;
using Sitecore.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sitecore.Support.Data.Items
{
    public sealed class ItemUtil
    {
        private static readonly string DefaultItemName = "Unnamed item";
        private static readonly string[][] EncodedReplacements;

        static ItemUtil()
        {
            string[] encodeNameReplacements = MainUtil.EncodeNameReplacements;
            EncodedReplacements = new string[encodeNameReplacements.Length / 2][];
            for (int i = 0; i < EncodedReplacements.Length; i++)
            {
                EncodedReplacements[i] = new string[] { encodeNameReplacements[i * 2], encodeNameReplacements[(i * 2) + 1] };
            }
        }

        public static Item AddFromTemplate(string itemName, string templateName, Item parent)
        {
            Sitecore.Diagnostics.Error.AssertString(itemName, "itemName", false);
            Sitecore.Diagnostics.Error.AssertString(templateName, "templateName", false);
            Sitecore.Diagnostics.Error.AssertObject(parent, "parent");
            TemplateItem template = parent.Database.Templates[templateName];
            return ((template == null) ? null : parent.Add(itemName, template));
        }

        public static void AssertDuplicateItemName(Item destinationItem, string name)
        {
            if (!Settings.AllowDuplicateItemNamesOnSameLevel)
            {
                foreach (Item item in destinationItem.Children)
                {
                    if (string.Equals(item.Name, name, StringComparison.InvariantCulture))
                    {
                        throw new DuplicateItemNameException(string.Format(Translate.Text("The item name \"{0}\" is already defined on this level."), name));
                    }
                }
            }
        }

        public static void AssertItemName(string name)
        {
            AssertItemName(null, name);
        }

        public static void AssertItemName(Item destinationItem, string name)
        {
            string itemNameError = GetItemNameError(name);
            if (itemNameError.Length > 0)
            {
                throw new InvalidItemNameException(itemNameError);
            }
            if (destinationItem != null)
            {
                AssertDuplicateItemName(destinationItem, name);
            }
        }

        private static void CleanupInheritedItems(Item item)
        {
            Item[] itemArray = item.Database.SelectItems($"fast:/sitecore/content//*[@@templateid='{item.TemplateID}']");
            if (itemArray != null)
            {
                using (new StatisticDisabler(StatisticDisablerState.ForItemsWithoutVersionOnly))
                {
                    foreach (Item item2 in itemArray)
                    {
                        Field field = item2.Fields[FieldIDs.LayoutField];
                        Field field2 = item2.Fields[FieldIDs.FinalLayoutField];
                        using (new EditContext(item2))
                        {
                            if (field.HasValue)
                            {
                                Sitecore.Support.Data.Fields.LayoutField.SetFieldValue(field, CleanupLayoutValue(Sitecore.Support.Data.Fields.LayoutField.GetFieldValue(field)));
                            }
                            if (field2.HasValue)
                            {
                                Sitecore.Support.Data.Fields.LayoutField.SetFieldValue(field2, CleanupLayoutValue(Sitecore.Support.Data.Fields.LayoutField.GetFieldValue(field2)));
                            }
                        }
                    }
                }
            }
        }

        private static string CleanupLayoutValue(string layout)
        {
            if (!string.IsNullOrEmpty(layout))
            {
                layout = LayoutDefinition.Parse(layout).ToXml();
            }
            return layout;
        }

        public static bool ContainsNonASCIISymbols(string input)
        {
            Assert.ArgumentNotNull(input, "input");
            return input.Any<char>(c => (c > '\x00ff'));
        }

        private static string ConvertToASCII(string str) =>
            Encoding.ASCII.GetString(Encoding.ASCII.GetBytes(str));

        public static Item CopyItem(Item source, Item destination, bool saveIds, bool deep)
        {
            Assert.ArgumentNotNull(source, "source");
            Assert.ArgumentNotNull(destination, "destination");
            Assert.IsTrue((source.Database.Name != destination.Database.Name) || !saveIds, "Cannot save values of item ids if 'source' and 'destination' are in the same database.");
            Item item = null;
            try
            {
                item = ItemManager.CreateItem(source.Name, destination, source.TemplateID, saveIds ? source.ID : ID.NewID);
                Assert.IsNotNull(item, "Item hasn't been created");
                item.Versions.RemoveAll(true);
                Item[] versions = source.Versions.GetVersions(true);
                int index = 0;
                while (true)
                {
                    if (index >= versions.Length)
                    {
                        if (versions.Length == 0)
                        {
                            item.Editing.BeginEdit();
                            item.BranchId = item.InnerData.Definition.BranchId;
                            item.RuntimeSettings.ReadOnlyStatistics = true;
                            item.Editing.EndEdit();
                        }
                        if (deep)
                        {
                            foreach (Item item4 in source.Children)
                            {
                                CopyItem(item4, item, saveIds, true);
                            }
                        }
                        break;
                    }
                    Item item2 = versions[index];
                    CoreItem.Builder builder = new CoreItem.Builder(item.ID, item.Name, item.TemplateID, item.Database.DataManager);
                    builder.SetVersion(item2.Version);
                    builder.SetLanguage(item2.Language);
                    if (!source.BranchId.IsNull)
                    {
                        builder.SetBranchId(source.BranchId);
                    }
                    item2.Fields.ReadAll();
                    foreach (Field field in item2.Fields)
                    {
                        string fieldValue = field.GetValue(false, false);
                        if (fieldValue != null)
                        {
                            builder.AddField(field.ID, fieldValue);
                        }
                    }
                    Item item3 = new Item(item.ID, builder.ItemData, item.Database);
                    item3.Editing.BeginEdit();
                    item3.RuntimeSettings.ReadOnlyStatistics = true;
                    item3.RuntimeSettings.SaveAll = true;
                    if (index != (versions.Length - 1))
                    {
                        item3.Editing.EndEdit(false, true);
                    }
                    else
                    {
                        item3.BranchId = item.InnerData.Definition.BranchId;
                        item3.Editing.EndEdit();
                    }
                    index++;
                }
            }
            catch (Exception exception)
            {
                Log.Error("Failed to copy an item", exception, typeof(ItemUtil));
                if (item != null)
                {
                    try
                    {
                        item.Delete();
                    }
                    catch (Exception)
                    {
                    }
                }
                throw;
            }
            return item;
        }

        public static void DeleteItem(ID itemID, Database database)
        {
            Sitecore.Diagnostics.Error.AssertObject(itemID, "itemID");
            Sitecore.Diagnostics.Error.AssertObject(database, "database");
            Item item = database.Items[itemID];
            if (item != null)
            {
                item.Delete();
            }
        }

        public static void DeleteItem(string path, Database database)
        {
            Sitecore.Diagnostics.Error.AssertString(path, "path", false);
            Sitecore.Diagnostics.Error.AssertObject(database, "database");
            Item item = database.Items[path];
            if (item != null)
            {
                item.Delete();
            }
        }

        public static List<Item> GetChildrenAt(string path)
        {
            Assert.ArgumentNotNullOrEmpty(path, "path");
            Database database = Context.Database;
            if (database == null)
            {
                return new List<Item>();
            }
            Item item = database.GetItem(path);
            return ((item != null) ? new List<Item>(item.Children.ToArray()) : new List<Item>());
        }

        public static string GetCopyOfName(Item destination, string name)
        {
            Sitecore.Diagnostics.Error.AssertObject(destination, "dest");
            Sitecore.Diagnostics.Error.AssertString(name, "name", false);
            string itemName = name;
            if (destination.Axes.GetChild(itemName) != null)
            {
                itemName = Translate.Text("Copy of") + " " + name;
                for (int i = 1; destination.Axes.GetChild(itemName) != null; i++)
                {
                    object[] objArray1 = new object[] { Translate.Text("Copy of"), " ", name, " ", i };
                    itemName = string.Concat(objArray1);
                }
            }
            return itemName;
        }

        public static Item GetItemFromPartialPath(string partialPath, Database database)
        {
            Assert.ArgumentNotNull(partialPath, "partialPath");
            Assert.ArgumentNotNull(database, "database");
            foreach (string str2 in MediaManager.Config.MediaPrefixes)
            {
                if (partialPath.StartsWith(str2, StringComparison.InvariantCultureIgnoreCase))
                {
                    string text1 = "/sitecore/media library/" + StringUtil.Mid(partialPath, str2.Length);
                    partialPath = text1;
                }
            }
            string id = StringUtil.Right(partialPath, 0x20);
            if (ShortID.IsShortID(id))
            {
                partialPath = ShortID.Decode(id);
            }
            Item item = database.GetItem(partialPath);
            SiteContext site = Context.Site;
            if ((item == null) && (site != null))
            {
                string path = FileUtil.MakePath(site.StartPath, partialPath);
                item = database.GetItem(path);
            }
            if (item == null)
            {
                string path = FileUtil.MakePath("/sitecore/content", partialPath);
                item = database.GetItem(path);
            }
            return item;
        }

        public static ID GetItemID(string itemPath, Database database)
        {
            Sitecore.Diagnostics.Error.AssertString(itemPath, "itemPath", false);
            Sitecore.Diagnostics.Error.AssertObject(database, "database");
            Item item = database.Items[itemPath];
            return ((item == null) ? ID.Null : item.ID);
        }

        public static string GetItemNameError(string name)
        {
            Sitecore.Diagnostics.Error.AssertString(name, "name", true);
            if (name.Length == 0)
            {
                return Translate.Text("An item name cannot be blank.");
            }
            if (name.Length > Settings.MaxItemNameLength)
            {
                return Translate.Text($"An item name lenght should be less or equal to {Settings.MaxItemNameLength}.");
            }
            if (name[name.Length - 1] == '.')
            {
                return Translate.Text("An item name cannot end in a period (.)");
            }
            if (name.Length != name.Trim().Length)
            {
                return Translate.Text("An item name cannot start or end with blanks.");
            }
            if (name.IndexOfAny(Settings.InvalidItemNameChars) >= 0)
            {
                string str2 = new string(Settings.InvalidItemNameChars);
                return string.Format(Translate.Text("An item name cannot contain any of the following characters: {0} (controlled by the setting InvalidItemNameChars)"), str2);
            }
            string itemNameValidation = Settings.ItemNameValidation;
            if ((itemNameValidation.Length > 0) && !Regex.IsMatch(name, itemNameValidation, RegexOptions.ECMAScript))
            {
                return string.Format(Translate.Text("An item name must satisfy the pattern: {0} (controlled by the setting ItemNameValidation)"), itemNameValidation);
            }
            if (!Settings.ItemNameAllowMixingReplacementCharacters)
            {
                string str3 = name.ToLowerInvariant();
                foreach (string[] strArray2 in EncodedReplacements)
                {
                    if (str3.Contains(strArray2[0]) && str3.Contains(strArray2[1]))
                    {
                        object[] parameters = new object[] { strArray2[0], strArray2[1] };
                        return Translate.Text("An item name cannot contain the both symbol sets replaced and replacing. The invalid sets are '{0}' and '{1}'", parameters);
                    }
                }
            }
            return string.Empty;
        }

        private static IEnumerable<Field> GetLayoutFieldsToReset(Item item, bool resetShared, ResetFinalLayoutOptions resetFinal)
        {
            List<Field> list = new List<Field>();
            if (resetShared)
            {
                list.Add(item.Fields[FieldIDs.LayoutField]);
            }
            if (resetFinal == ResetFinalLayoutOptions.ThisVersion)
            {
                if (item.Versions.Count > 0)
                {
                    list.Add(item.Fields[FieldIDs.FinalLayoutField]);
                }
            }
            else if (resetFinal == ResetFinalLayoutOptions.ThisLanguage)
            {
                list.AddRange(from v in item.Versions.GetVersions(false) select v.Fields[FieldIDs.FinalLayoutField]);
            }
            else if (resetFinal == ResetFinalLayoutOptions.All)
            {
                list.AddRange(from v in item.Versions.GetVersions(true) select v.Fields[FieldIDs.FinalLayoutField]);
            }
            return list;
        }

        public static int GetLevel(Item itm)
        {
            int num = 0;
            for (Item item = itm.Parent; item != null; item = item.Parent)
            {
                num++;
            }
            return num;
        }

        public static int GetLevel(Item item, out bool isTemplate)
        {
            Assert.ArgumentNotNull(item, "item");
            string longID = item.Paths.LongID;
            isTemplate = longID.Contains(ItemIDs.TemplateRoot.ToString());
            char[] separator = new char[] { '/' };
            return (longID.Split(separator, StringSplitOptions.RemoveEmptyEntries).Length - 1);
        }

        public static ID GetParentID(Item item)
        {
            Item parent = item.Parent;
            return ((parent == null) ? ID.Null : parent.ID);
        }

        public static string GetParentName(Item item)
        {
            Sitecore.Diagnostics.Error.AssertObject(item, "item");
            return GetParentName(item, false);
        }

        public static string GetParentName(Item item, bool useDisplayName)
        {
            Sitecore.Diagnostics.Error.AssertObject(item, "item");
            Item parent = item.Parent;
            return ((parent == null) ? string.Empty : (!useDisplayName ? parent.Name : parent.DisplayName));
        }

        public static string GetTemplateKey(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            return item.TemplateName.ToLowerInvariant();
        }

        public static string GetUniqueName(Item parent, string name)
        {
            Assert.ArgumentNotNull(parent, "parent");
            Assert.ArgumentNotNull(name, "name");
            string itemName = name;
            for (int i = 1; parent.Axes.GetChild(itemName) != null; i++)
            {
                itemName = name + " " + i;
            }
            return itemName;
        }

        public static bool IsDataField(TemplateField templateField)
        {
            Assert.ArgumentNotNull(templateField, "templateField");
            return ((templateField.Template.ID != TemplateIDs.StandardTemplate) && (templateField.Template.BaseIDs.Length != 0));
        }

        public static bool IsItemNameValid(string name)
        {
            Sitecore.Diagnostics.Error.AssertString(name, "name", true);
            return (GetItemNameError(name).Length == 0);
        }

        [Obsolete("Please use ID.IsNullOrEmpty(id) API instead.")]
        public static bool IsNull(ID id) =>
            ID.IsNullOrEmpty(id);

        public static bool IsRenderingItem(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            ID templateId = new ID("{D1592226-3898-4CE2-B190-090FD5F84A4C}");
            Template template = TemplateManager.GetTemplate(item);
            return ((template != null) ? template.DescendsFrom(templateId) : false);
        }

        public static string ProposeValidItemName(string name)
        {
            Assert.ArgumentNotNullOrEmpty(name, "name");
            return Assert.ResultNotNull<string>(ProposeValidItemName(name, DefaultItemName));
        }

        public static string ProposeValidItemName(string name, string defaultValue)
        {
            Assert.ArgumentNotNull(name, "name");
            if (IsItemNameValid(name))
            {
                return name;
            }
            string str = ConvertToASCII(name.Trim());
            foreach (char ch in Settings.InvalidItemNameChars)
            {
                str = str.Replace(ch.ToString(), string.Empty);
            }
            str = str.Trim();
            if (IsItemNameValid(str))
            {
                return str;
            }
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    builder.Append(" ");
                }
            }
            str = builder.ToString().Trim();
            if (str.Length > Settings.MaxItemNameLength)
            {
                str = str.Remove(Settings.MaxItemNameLength).Trim();
            }
            if (IsItemNameValid(str))
            {
                return str;
            }
            Log.Warn($"Cannot create a valid item name from string '{name}'", typeof(ItemUtil));
            if (!IsItemNameValid(defaultValue))
            {
                throw new Exception("Cannot create a valid item name. Please check the related setting in web.config file");
            }
            return defaultValue;
        }

        public static void ResetLayoutDetails(Item item, bool resetShared, ResetFinalLayoutOptions resetFinal)
        {
            Assert.ArgumentNotNull(item, "item");
            using (new StatisticDisabler(StatisticDisablerState.ForItemsWithoutVersionOnly))
            {
                foreach (Field field in GetLayoutFieldsToReset(item, resetShared, resetFinal))
                {
                    field.Item.Editing.BeginEdit();
                    field.Reset();
                    field.Item.Editing.EndEdit();
                }
            }
        }

        public static void SetLayoutDetails(Item item, string sharedLayout, string finalLayout)
        {
            Assert.ArgumentNotNull(item, "item");
            Assert.ArgumentNotNull(sharedLayout, "sharedLayout");
            Assert.ArgumentNotNull(finalLayout, "finalLayout");
            string str = sharedLayout + finalLayout;
            string text1 = CleanupLayoutValue(sharedLayout);
            sharedLayout = text1;
            string text2 = CleanupLayoutValue(finalLayout);
            finalLayout = text2;
            using (new StatisticDisabler(StatisticDisablerState.ForItemsWithoutVersionOnly))
            {
                item.Editing.BeginEdit();
                Field field = item.Fields[FieldIDs.LayoutField];
                if (!XmlUtil.XmlStringsAreEqual(CleanupLayoutValue(Sitecore.Support.Data.Fields.LayoutField.GetFieldValue(field)), sharedLayout))
                {
                    Sitecore.Support.Data.Fields.LayoutField.SetFieldValue(field, sharedLayout);
                }
                if (!item.RuntimeSettings.TemporaryVersion)
                {
                    Sitecore.Support.Data.Fields.LayoutField.SetFieldValue(item.Fields[FieldIDs.FinalLayoutField], finalLayout, sharedLayout);
                }
                item.Editing.EndEdit();
            }
            if (item.Name == "__Standard Values")
            {
                CleanupInheritedItems(item);
            }
            string[] parameters = new string[] { AuditFormatter.FormatItem(item), str };
            Log.Audit(typeof(ItemUtil), "Set layout details: {0}, layout: {1}", parameters);
        }

    }
}
