using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;

namespace Heleus.Apps.Shared
{
    public class ChatsListView : StackListView<ChatProfileButtonRow, Chat>
    {
        public ChatsListView(StackPage page, StackRow header) : base(page, header)
        {
            //UIApp.PubSub.Subscribe<ChatMessageDownloadEvent>(this, UpdateCount);
        }

        Task UpdateCount(ChatMessageDownloadEvent arg)
        {
            var rows = Rows;

            foreach(var row in rows)
            {
                var chatRow = row as ChatProfileButtonRow;
                var item = row.Tag as Chat;

                if(chatRow != null && item != null)
                    chatRow.UpdateMessagesCount(item.LastCount - item.LastViewedCount);
            }

            return Task.CompletedTask;
        }

        protected override ChatProfileButtonRow AddRow(StackPage page, Chat item)
        {
            var friend = item.Node.GetFriend(item.FriendAccountId);
            if (friend == null)
                return null;

            var row = page.AddRow(new ChatProfileButtonRow(item.Node.ServiceNode, friend.AccountId, friend.Profile, ProfileManager.Current.GetCachedProfileData(friend.AccountId), OpenChat, true));
            row.UpdateMessagesCount(item.LastCount - item.LastViewedCount);
            return row;
        }

        async Task OpenChat(ProfileButtonRow arg)
        {
            var row = arg as ChatProfileButtonRow;
            var item = arg.Tag as Chat;

            var update = item.UpdateLastViewedCount();

            await _page.Navigation.PushAsync(new ChatPage(item));

            if(update)
            {
                row.UpdateMessagesCount(item.LastCount - item.LastViewedCount);
                await item.Node.SaveAsync();
            }
        }

        protected override void UpdateRow(ChatProfileButtonRow row, Chat newItem)
        {
            row.UpdateMessagesCount(0);
            row.AccountId = newItem.FriendAccountId;
        }

        protected override void UpdateDone()
        {
            base.UpdateDone();
            UpdateCount(null);
        }
    }
}
