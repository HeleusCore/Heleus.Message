using Heleus.Chain.Data;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Message
{
    public class FriendsDownloadEvent : MessageNodeResultEvent
    {
        public readonly FriendInfo FriendInfo;

        public FriendsDownloadEvent(FriendInfo friendInfo, MessageNode node, MessageNodeEventResultTypes resultTypes) : base(node, resultTypes)
        {
            FriendInfo = friendInfo;
        }
    }
}
