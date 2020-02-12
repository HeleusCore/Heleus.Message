using Heleus.Network.Client;

namespace Heleus.Apps.Message
{
    public class SendInvitationEvent : MessageNodeClientEvent
    {
        public readonly long AccountId;

        public SendInvitationEvent(long accountId, MessageNode node, HeleusClientResponse heleusClientResponse) : base(node, heleusClientResponse)
        {
            AccountId = accountId;
        }
    }
}
