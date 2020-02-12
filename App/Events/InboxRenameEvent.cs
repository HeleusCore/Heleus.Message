using Heleus.Network.Client;

namespace Heleus.Apps.Message
{
    public class InboxRenameEvent : MessageNodeClientEvent
    {
        public readonly string Title;
        public readonly short KeyIndex;

        public InboxRenameEvent(string title, short keyIndex, MessageNode node, HeleusClientResponse heleusClientResponse) : base(node, heleusClientResponse)
        {
            Title = title;
            KeyIndex = keyIndex;
        }
    }
}
