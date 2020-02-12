using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Base;
using Heleus.MessageService;
using Heleus.Network.Client;
using Heleus.ProfileService;
using Heleus.Transactions;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Message
{
    public class Chat : IPackable, IUnpackerKey<Chain.Index>
    {
        public readonly Chain.Index Index;
        public readonly MessageNode Node;
        ServiceNode _serviceNode => Node.ServiceNode;

        public Chain.Index UnpackerKey => Index;

        public readonly SharedAccountIndexTransactionDownload Download;

        public readonly long FriendAccountId;
        public readonly short FriendKeyIndex;
        public readonly long AccountId;
        public readonly short KeyIndex;

        public long LastCount { get; private set; }
        public long LastViewedCount { get; private set; } = -1;
        public long LastTransactionId { get; private set; }
        public long LastTimestamp { get; private set; }

        readonly Dictionary<long, DecryptedRecordData<MessageRecord>> _messages = new Dictionary<long, DecryptedRecordData<MessageRecord>>();

        public Chat(Chain.Index index, MessageNode node)
        {
            Index = index;
            Node = node;
            Download = new SharedAccountIndexTransactionDownload(node.AccountId, index, node.ServiceNode.GetTransactionDownloadManager(MessageServiceInfo.MessageDataChainIndex))
            {
                Count = 20
            };

            (FriendAccountId, FriendKeyIndex, AccountId, KeyIndex) = node.GetFriendAccountId(index);
        }

        public Chat(Unpacker unpacker, MessageNode node) : this(new Chain.Index(unpacker), node)
        {
            LastTransactionId = unpacker.UnpackLong();
            LastTimestamp = unpacker.UnpackLong();
            LastCount = unpacker.UnpackLong();
            LastViewedCount = unpacker.UnpackLong();
        }

        public DecryptedRecordData<MessageRecord> GetMessage(TransactionDownloadData<Transaction> transaction)
        {
            if(!_messages.TryGetValue(transaction.TransactionId, out var record))
            {
                record = new DecryptedRecordData<MessageRecord>(transaction, _serviceNode, Index, "message", DecryptedRecordDataSource.DataFeature);
                _messages[transaction.TransactionId] = record;
            }

            return record;
        }

        public void Pack(Packer packer)
        {
            packer.Pack(Index);
            packer.Pack(LastTransactionId);
            packer.Pack(LastTimestamp);
            packer.Pack(LastCount);
            packer.Pack(LastViewedCount);
        }

        bool _busy;

        public async Task<SharedAccountIndexTransactionDownload> QueryStoredMessages()
        {
            if (_busy)
                return Download;
            _busy = true;

            await Download.QueryStoredTransactions();

            _busy = false;
            await UIApp.PubSub.PublishAsync(new ChatMessageDownloadEvent(this, Node));
            return Download;
        }

        public bool UpdateLastViewedCount()
        {
            if (LastViewedCount == LastCount)
                return false;

            LastViewedCount = LastCount;
            return true;
        }

        public async Task<SharedAccountIndexTransactionDownload> DownloadMessages(bool queryOlder)
        {
            if (_busy)
                return Download;
            _busy = true;

            Download.QueryOlder = queryOlder;
            await Download.DownloadTransactions();

            foreach(var transaction in Download.Transactions.Values)
            {
                var shared = transaction.Transaction.GetFeature<SharedAccountIndex>(SharedAccountIndex.FeatureId);

                LastTransactionId = Math.Max(transaction.TransactionId, LastTransactionId);
                LastCount = Math.Max(shared.TransactionCount, LastCount);
                LastTimestamp = Math.Max(transaction.Transaction.Timestamp, LastTimestamp);
            }

            if (LastViewedCount < 0 && LastCount > 0)
                LastViewedCount = LastCount;

            _busy = false;
            await UIApp.PubSub.PublishAsync(new ChatMessageDownloadEvent(this, Node));
            return Download;
        }
    }
}