using Heleus.Network.Client;

namespace Heleus.Apps.Message
{
    public class MessageSentEvent : MessageNodeClientEvent
    {
        public readonly string Text;
        public readonly Chain.Index Index;

        public MessageSentEvent(string text, Chain.Index index, MessageNode node, HeleusClientResponse heleusClientResponse) : base(node, heleusClientResponse)
        {
            Text = text;
            Index = index;
        }
    }
}
