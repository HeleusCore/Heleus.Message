using Heleus.Apps.Shared;
using Heleus.Chain;
using Heleus.Cryptography;

namespace Heleus.Apps.Message
{
    public class MessageSubmitAccount : SubmitAccount
    {
        public readonly long FriendAccountId;
        public readonly short FriendKeyIndex;

        public readonly KeyStore Account;
        public readonly MessageNode Node;

        public MessageSubmitAccount(KeyStore account, MessageNode node, long receiverAccountId, short receiverKeyIndex, short keyIndex, Chain.Index index, bool requiresSecretKey) : base(node.ServiceNode, keyIndex, index, requiresSecretKey)
        {
            Account = account;
            FriendAccountId = receiverAccountId;
            FriendKeyIndex = receiverKeyIndex;
            Node = node;
        }

        public override string Name
        {
            get
            {
                string inboxName = null;

                foreach(var record in Node.InboxNameRecords)
                {
                    if(record.KeyIndex == KeyIndex)
                    {
                        inboxName = record.Title;
                        break;
                    }
                }

                if (inboxName == null)
                    inboxName = Tr.Get("Common.Inbox");

                return inboxName;
            }
        }

        public override string Detail
        {
            get
            {
                return Tr.Get("Common.InboxName", Node.AccountId, KeyIndex);
            }
        }
    }
}
