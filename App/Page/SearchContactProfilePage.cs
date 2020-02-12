using System.Threading.Tasks;
using Heleus.Apps.Message;

namespace Heleus.Apps.Shared
{
    public class SearchContactProfilePage : SearchProfilePage
    {
        readonly MessageNode _node;

        public SearchContactProfilePage(MessageNode node) : base(new ServiceProfileSearch(node.ServiceNode))
        {
            _node = node;
        }

        protected override async Task ProfileButton(ProfileButtonRow arg)
        {
            await Navigation.PushAsync(new ContactPage(_node, arg.AccountId));
        }
    }
}
