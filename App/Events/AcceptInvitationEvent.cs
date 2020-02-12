using Heleus.Network.Client;

namespace Heleus.Apps.Message
{
    public class AcceptInvitationEvent : MessageNodeClientEvent
    {
        public readonly long AccountId;

        public AcceptInvitationEvent(long accountId, MessageNode node, HeleusClientResponse heleusClientResponse) : base(node, heleusClientResponse)
        {
            AccountId = accountId;
        }
    }

    public class UnfriendEvent : MessageNodeClientEvent
    {
        public readonly long AccountId;

        public UnfriendEvent(long accountId, MessageNode node, HeleusClientResponse heleusClientResponse) : base(node, heleusClientResponse)
        {
            AccountId = accountId;
        }
    }
}
