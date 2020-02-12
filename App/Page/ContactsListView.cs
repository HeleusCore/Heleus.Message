using System.Threading.Tasks;
using Heleus.Apps.Message;

namespace Heleus.Apps.Shared
{
    public class ContactsListView : StackListView<ProfileButtonRow, long>
    {
        public readonly MessageNode Node;

        public ContactsListView(MessageNode node, StackPage page, HeaderRow header) : base(page, header)
        {
            Node = node;
        }

        async Task Friend(ProfileButtonRow arg)
        {
            if (Node != null)
                await _page.Navigation.PushAsync(new ContactPage(Node, (long)arg.Tag));
        }

        protected override ProfileButtonRow AddRow(StackPage page, long item)
        {
            return _page.AddRow(new ProfileButtonRow(item, Friend));
        }

        protected override void UpdateRow(ProfileButtonRow row, long newItem)
        {
            row.AccountId = newItem;
        }
    }
}
