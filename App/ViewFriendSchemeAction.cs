using Heleus.MessageService;
using System.Threading.Tasks;
#if !(GTK || CLI)
#endif

namespace Heleus.Apps.Shared
{
    public class ViewFriendSchemeAction : ServiceNodeSchemeAction
    {
        public const string ActionName = "viewfriend";

        public readonly long AccountId;
        public readonly short KeyIndex;

        public override bool IsValid => base.IsValid && AccountId > 0;

        public ViewFriendSchemeAction(SchemeData schemeData) : base(schemeData)
        {
            GetLong(StartIndex, out AccountId);
            if (!GetShort(StartIndex + 1, out KeyIndex))
                KeyIndex = -1;
        }

        public override async Task Run()
        {
            if (!IsValid)
                return;

            var serviceNode = await GetServiceNode();
            if (serviceNode == null)
                return;

            var node = MessageApp.Current.GetNode(serviceNode);
            if (node == null)
                return;

            var app = UIApp.Current;
            if (app != null)
            {
                app.MainTabbedPage?.ShowPage(typeof(ChatsPage));
                await UIApp.Current.CurrentPage.Navigation.PushAsync(new ContactPage(node, AccountId, KeyIndex, node.GetChat(AccountId, KeyIndex)));
            }
        }
    }
}
