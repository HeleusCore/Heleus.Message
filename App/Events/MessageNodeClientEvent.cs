using Heleus.Apps.Shared;
using Heleus.Network.Client;

namespace Heleus.Apps.Message
{
    public class MessageNodeClientEvent : ClientResponseEvent
    {
        public readonly MessageNode Node;

        public MessageNodeClientEvent(MessageNode node, HeleusClientResponse heleusClientResponse) : base(heleusClientResponse)
        {
            Node = node;
        }
    }
}
