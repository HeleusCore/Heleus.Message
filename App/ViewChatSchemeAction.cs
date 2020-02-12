using Heleus.MessageService;
using System.Threading.Tasks;
#if !(GTK || CLI)
#endif

namespace Heleus.Apps.Shared
{
    public class ViewChatSchemeAction : ServiceNodeSchemeAction
    {
        public const string ActionName = "viewchat";

        public readonly Chain.Index Index;

        public override bool IsValid => base.IsValid && MessageServiceInfo.IsValidConversationIndex(Index);

        public ViewChatSchemeAction(SchemeData schemeData) : base(schemeData)
        {
            Index = new Chain.Index(GetString(StartIndex));
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
                var chat = node.GetChat(Index, true);
                await UIApp.Current.CurrentPage.Navigation.PushAsync(new ChatPage(chat));
            }
        }
    }
}
