using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Shell.Applications.Dialogs.LayoutDetails;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using System.Collections.Specialized;

namespace Sitecore.Support.Commands
{
    public class SetLayoutDetails : Command
    {
        // Methods
        public override void Execute(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            Error.AssertObject(context, "context");
            if (context.Items.Length == 1)
            {
                Item item = context.Items[0];
                NameValueCollection parameters = new NameValueCollection
                {
                    ["id"] = item.ID.ToString(),
                    ["language"] = item.Language.ToString(),
                    ["version"] = item.Version.ToString(),
                    ["database"] = item.Database.Name
                };
                Context.ClientPage.Start(this, "Run", parameters);
            }
        }

        public override CommandState QueryState(CommandContext context)
        {
            Assert.ArgumentNotNull(context, "context");
            if (context.Items.Length != 1)
            {
                return CommandState.Hidden;
            }
            Item item = context.Items[0];
            if (!base.HasField(item, FieldIDs.LayoutField))
            {
                return CommandState.Hidden;
            }
            if (((WebUtil.GetQueryString("mode") == "preview") || (!item.Access.CanWrite() || item.Appearance.ReadOnly)) || !item.Access.CanWriteLanguage())
            {
                return CommandState.Disabled;
            }
            return base.QueryState(context);
        }

        protected virtual void Run(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            CheckModifiedParameters parameters = new CheckModifiedParameters();
            parameters.ResumePreviousPipeline = true;
            if (SheerResponse.CheckModified(parameters))
            {
                if (!args.IsPostBack)
                {
                    UrlString str = new UrlString(UIUtil.GetUri("control:LayoutDetails"));
                    str.Append("id", args.Parameters["id"]);
                    str.Append("la", args.Parameters["language"]);
                    str.Append("vs", args.Parameters["version"]);
                    SheerResponse.ShowModalDialog(str.ToString(), "650px", string.Empty, string.Empty, true);
                    args.WaitForPostBack();
                }
                else if (args.HasResult)
                {
                    Database database = Factory.GetDatabase(args.Parameters["database"]);
                    Assert.IsNotNull(database, "Database \"" + args.Parameters["database"] + "\" not found.");
                    Item item = database.GetItem(ID.Parse(args.Parameters["id"]), Language.Parse(args.Parameters["language"]), Sitecore.Data.Version.Parse(args.Parameters["version"]));
                    Assert.IsNotNull(item, "item");
                    LayoutDetailsDialogResult result = LayoutDetailsDialogResult.Parse(args.Result);


                    Sitecore.Support.Data.Items.ItemUtil.SetLayoutDetails(item, result.Layout, result.FinalLayout);
                    if (result.VersionCreated)
                    {
                        object[] objArray1 = new object[] { "item:versionadded(id=", item.ID, ",version=", item.Version, ",language=", item.Language, ")" };
                        Context.ClientPage.SendMessage(this, string.Concat(objArray1));
                    }
                }
            }
        }
    }

}
