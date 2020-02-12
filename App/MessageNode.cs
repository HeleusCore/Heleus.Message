using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Base;
using Heleus.Chain;
using Heleus.Chain.Data;
using Heleus.Cryptography;
using Heleus.MessageService;
using Heleus.Network.Client;
using Heleus.Network.Client.Record;
using Heleus.Network.Results;
using Heleus.ProfileService;
using Heleus.Transactions;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Message
{
    public class MessageNode : NodeBase
    {
        FriendInfo _friendInfo;
        readonly Dictionary<long, Friend> _friends = new Dictionary<long, Friend>();
        readonly Dictionary<Chain.Index, Chat> _chats = new Dictionary<Chain.Index, Chat>();

        public ICollection<Chat> Chats => _chats.Values;

        LastTransactionCountInfo _lastAccountTransaction;
        LastTransactionCountInfo _lastReceivedTransaction;

        public IReadOnlyList<InboxNameRecordInfo> InboxNameRecords { get; private set; }

        public ProfileInfo ProfileInfo { get; private set; }

        public static Task<MessageNode> LoadAsync(ServiceNode serviceNode)
        {
            return Task.Run(() => new MessageNode(serviceNode));
        }

        public MessageNode(ServiceNode serviceNode) : base(serviceNode)
        {
            try
            {
                var data = serviceNode.CacheStorage.ReadFileBytes(GetType().Name);
                if (data != null)
                {
                    using (var unpacker = new Unpacker(data))
                    {
                        if (unpacker.UnpackBool())
                            ProfileInfo = new ProfileInfo(unpacker);

                        unpacker.Unpack(_friends, (u) => new Friend(u, this));
                        unpacker.Unpack(_chats, (u) => new Chat(u, this));
                        if (unpacker.UnpackBool())
                            _friendInfo = new FriendInfo(unpacker);
                        if (unpacker.UnpackBool())
                            _lastAccountTransaction = new LastTransactionCountInfo(unpacker);
                        if (unpacker.UnpackBool())
                            _lastReceivedTransaction = new LastTransactionCountInfo(unpacker);

                        InboxNameRecords = unpacker.UnpackList((u) => new InboxNameRecordInfo(u));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.IgnoreException(ex);
            }

            if (InboxNameRecords == null)
                InboxNameRecords = new List<InboxNameRecordInfo>();

            foreach (var inbox in _chats.Values)
            {
                GenerateSubmitAccount(inbox.Index);
            }

            UIApp.Run(GenerateDefaultExchangeKeys);
        }

        public override void Pack(Packer packer)
        {
            if (packer.Pack(ProfileInfo != null))
                packer.Pack(ProfileInfo);

            packer.Pack(_friends);
            packer.Pack(_chats);
            if (packer.Pack(_friendInfo != null))
                packer.Pack(_friendInfo);
            if (packer.Pack(_lastAccountTransaction != null))
                packer.Pack(_lastAccountTransaction);
            if (packer.Pack(_lastReceivedTransaction != null))
                packer.Pack(_lastReceivedTransaction);
            packer.Pack(InboxNameRecords);
        }

        public void Init()
        {
            UIApp.Run(PollLoop);
        }

        async Task GenerateDefaultExchangeKeys()
        {
            var submitAccounts = ServiceNode.GetSubmitAccounts<MessageSubmitAccount>();

            foreach (var submitAccount in submitAccounts)
            {
                var index = submitAccount.Index;
                var serviceAccount = submitAccount.ServiceAccount as ServiceAccountKeyStore;
                var secretKeyManager = submitAccount.SecretKeyManager;

                if (!secretKeyManager.HasSecretKeyType(index, SecretKeyInfoTypes.KeyExchange))
                {
                    (var friendAccountId, var friendKeyIndex, _, _) = GetFriendAccountId(index);
                    await GenerateSecretExchangeKey(submitAccount, friendAccountId, friendKeyIndex);
                }
            }
        }

        public (long, short, long, short) GetFriendAccountId(Chain.Index index)
        {
            var a1 = index.GetLong(1);
            var k1 = index.GetShort(2);
            var a2 = index.GetLong(3);
            var k2 = index.GetShort(4);

            if (a1 != AccountId)
                return (a1, k1, a2, k2);

            return (a2, k2, a1, k2);
        }

        MessageSubmitAccount GenerateSubmitAccount(Chain.Index index)
        {
            (var friendAccountId, var friendKeyIndex, var accountId, var keyIndex) = GetFriendAccountId(index);

            if (accountId != AccountId)
                return null;

            var submitAccount = ServiceNode.GetSubmitAccount<MessageSubmitAccount>(keyIndex, index);
            if (submitAccount != null)
                return submitAccount;

            foreach (var account in ServiceNode.ServiceAccounts.Values)
            {
                if (!account.IsDecrypted)
                    continue;

                if (account.AccountId == accountId && account.KeyIndex == keyIndex)
                {
                    submitAccount = new MessageSubmitAccount(account, this, friendAccountId, friendKeyIndex, keyIndex, index, true);
                    ServiceNode.AddSubmitAccount(submitAccount);

                    return submitAccount;
                }
            }

            return null;
        }

        public bool GenerateSubmitAccounts(long friendAccountId, short friendKeyIndex)
        {
            foreach (var account in ServiceNode.ServiceAccounts.Values)
            {
                if (!account.IsDecrypted)
                    continue;

                var index = MessageServiceInfo.GetConversationIndex(account.AccountId, account.KeyIndex, friendAccountId, friendKeyIndex);
                var submitAccount = ServiceNode.GetSubmitAccount<MessageSubmitAccount>(account.KeyIndex, index);
                if (submitAccount == null)
                {
                    submitAccount = new MessageSubmitAccount(account, this, friendAccountId, friendKeyIndex, account.KeyIndex, index, true);
                    ServiceNode.AddSubmitAccount(submitAccount);
                }
            }

            return true;
        }

        async Task<bool> GenerateSecretExchangeKey(MessageSubmitAccount submitAccount, long friendAccountId, short friendKeyIndex)
        {
            if (submitAccount == null)
                return false;

            //await GenerateSubmitAccounts(friendAccountId, friendKeyIndex, false);

            var index = MessageServiceInfo.GetConversationIndex(submitAccount.AccountId, submitAccount.KeyIndex, friendAccountId, friendKeyIndex);

            var keyManager = submitAccount.SecretKeyManager;
            if (!keyManager.HasSecretKeyType(index, SecretKeyInfoTypes.KeyExchange))
            {
                var friend = GetFriend(friendAccountId);
                if (friend != null)
                {
                    var key = await friend.GetSignedPublicKey(friendKeyIndex);
                    if (key != null)
                    {
                        var exchangeKey = Key.KeyExchange(KeyTypes.Ed25519, submitAccount.Account.DecryptedKey, key.PublicKey);
                        var secretKey = await KeyExchageSecretKeyInfo.NewKeyExchangeSecetKey(submitAccount.AccountId, submitAccount.KeyIndex, friendAccountId, friendKeyIndex, submitAccount.ChainId, exchangeKey);
                        keyManager.AddSecretKey(index, secretKey);

                        return true;
                    }
                }

                return false;
            }

            return true;
        }

        public async Task GenerateSubmitAccountAndExchangeKey(Chat inbox)
        {
            var submitAccount = GenerateSubmitAccount(inbox.Index);
            await GenerateSecretExchangeKey(submitAccount, inbox.FriendAccountId, inbox.FriendKeyIndex);
        }

        public async Task<MessageSentEvent> SendMessage(Chat chat, string text)
        {
            var index = chat.Index;
            var submitAccount = ServiceNode.GetSubmitAccount<MessageSubmitAccount>(chat.KeyIndex, index);
            if (submitAccount == null)
                submitAccount = GenerateSubmitAccount(index);

            await GenerateSecretExchangeKey(submitAccount, chat.FriendAccountId, chat.FriendKeyIndex);

            var result = await SetSubmitAccount(submitAccount, true);
            if (result != null)
                goto end;

            if (string.IsNullOrWhiteSpace(text))
            {
                result = new HeleusClientResponse(HeleusClientResultTypes.Ok, (long)ServiceUserCodes.InvalidAttachement);
                goto end;
            }

            var secretKey = submitAccount.DefaultSecretKey;
            var record = await EncrytpedRecord<MessageRecord>.EncryptRecord(secretKey, new MessageRecord(text));

            var transaction = new DataTransaction(submitAccount.AccountId, submitAccount.ChainId, MessageServiceInfo.MessageDataChainIndex);

            transaction.EnableFeature<PreviousAccountTransaction>(PreviousAccountTransaction.FeatureId);

            var sharedAccountIndex = transaction.EnableFeature<SharedAccountIndex>(SharedAccountIndex.FeatureId);
            sharedAccountIndex.Index = index;

            var data = transaction.EnableFeature<Data>(Data.FeatureId);
            data.AddBinary(MessageServiceInfo.MessageDataIndex, record.ToByteArray());

            var receiver = transaction.EnableFeature<Receiver>(Receiver.FeatureId);
            receiver.AddReceiver(submitAccount.FriendAccountId);

            transaction.EnableFeature(EnforceReceiverFriend.FeatureId);

            result = await _client.SendDataTransaction(transaction, true);
            if (result.TransactionResult == TransactionResultTypes.Ok)
            {
                if (!_chats.ContainsKey(chat.Index))
                {
                    _chats[chat.Index] = chat;
                    await SaveAsync();
                }

                UIApp.Run(() => chat.DownloadMessages(false));
            }

        end:

            var @event = new MessageSentEvent(text, index, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        async Task QueryMissingProfiles()
        {
            if (_friendInfo != null)
            {
                foreach (var friendId in _friendInfo.Friends)
                {
                    var friend = GetFriend(friendId);
                    if(friend.Profile == null)
                        friend.Profile = (await ProfileManager.Current.GetProfileInfo(friendId, ProfileDownloadType.DownloadIfNotAvailable, false)).Profile;
                }

                foreach (var invitation in _friendInfo.Invitations)
                {
                    await ProfileManager.Current.GetProfileInfo(invitation.FriendAccountId, ProfileDownloadType.DownloadIfNotAvailable, false);
                }
            }
        }

        public Chat GetChat(long friendAccountId, short friendKeyIndex)
        {
            foreach (var chat in _chats.Values)
            {
                if (chat.FriendAccountId == friendAccountId && chat.FriendKeyIndex == friendKeyIndex)
                    return chat;
            }

            return null;
        }

        public Chat GetChat(Chain.Index index, bool createIfMissing)
        {
            if(_chats.TryGetValue(index, out var chat))
                return chat;

            if(createIfMissing)
                return new Chat(index, this);

            return null;
        }

        public List<Chat> GetChats(long friendAccountId)
        {
            var result = new List<Chat>();

            foreach(var chat in _chats.Values)
            {
                if (chat.FriendAccountId == friendAccountId)
                    result.Add(chat);
            }

            return result;
        }

        public bool IsNodeChat(long friendAccountId, short friendKeyIndex)
        {
            foreach(var chat in _chats.Values)
            {
                if (chat.FriendAccountId == friendAccountId && chat.FriendKeyIndex == friendKeyIndex)
                    return true;
            }

            return false;
        }

        public bool IsNodeChat(Chat chat)
        {
            if (chat != null)
                return IsNodeChat(chat.Index);

            return false;
        }

        public bool IsNodeChat(Chain.Index index)
        {
            return _chats.ContainsKey(index);
        }

        public async Task<bool> RemoveChat(Chat chat)
        {
            if (chat == null)
                return false;

            if (_chats.Remove(chat.Index))
            {
                await SaveAsync();
                await UIApp.PubSub.PublishAsync(new MessageNodeRefreshEvent(this));

                return true;
            }

            return false;
        }

        public Friend GetFriend(long accountId)
        {
            if (!_friends.TryGetValue(accountId, out var friend))
            {
                if (_friendInfo != null && _friendInfo.Friends.Contains(accountId))
                {
                    friend = new Friend(accountId, this);
                    _friends.Add(accountId, friend);
                }
            }
            return friend;
        }

        public FriendInvitation GetInvitation(long accountId)
        {
            if (_friendInfo != null)
            {
                foreach (var invitation in _friendInfo.Invitations)
                {
                    if (invitation.FriendAccountId == accountId)
                        return invitation;
                }
            }

            return null;
        }

        FeatureRequestDataTransaction NewFriendTransaction(long accountId, int chainId, FriendRequestMode friendRequestMode, long friendId)
        {
            var transaction = new FeatureRequestDataTransaction(accountId, chainId, MessageServiceInfo.FriendChainIndex);
            var request = new FriendRequest(friendRequestMode, friendId);

            transaction.SetFeatureRequest(request);

            return transaction;
        }

        public async Task<SendInvitationEvent> SendFriendInvitation(long friendId)
        {
            var submitAccount = ServiceNode.GetSubmitAccounts<SubmitAccount>(MessageServiceInfo.SubmitAccountIndex).FirstOrDefault();
            var result = await SetSubmitAccount(submitAccount);
            if (result != null)
                goto end;

            var transaction = NewFriendTransaction(AccountId, ChainId, FriendRequestMode.SendInvitation, friendId);

            result = await _client.SendDataTransaction(transaction, true);
            if (result.TransactionResult == TransactionResultTypes.Ok)
                UIApp.Run(() => DownloadFriends(false));

            end:
            var @event = new SendInvitationEvent(friendId, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        public async Task<AcceptInvitationEvent> AcceptFriendInvitation(long friendId)
        {
            var submitAccount = ServiceNode.GetSubmitAccounts<SubmitAccount>(MessageServiceInfo.SubmitAccountIndex).FirstOrDefault();
            var result = await SetSubmitAccount(submitAccount);
            if (result != null)
                goto end;

            var transaction = NewFriendTransaction(AccountId, ChainId, FriendRequestMode.AcceptInvitation, friendId);

            result = await _client.SendDataTransaction(transaction, true);
            if (result.TransactionResult == TransactionResultTypes.Ok)
                UIApp.Run(() => DownloadFriends(false));

            end:

            var @event = new AcceptInvitationEvent(friendId, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        public async Task<UnfriendEvent> Unfriend(long friendId)
        {
            var submitAccount = ServiceNode.GetSubmitAccounts<SubmitAccount>(MessageServiceInfo.SubmitAccountIndex).FirstOrDefault();
            var result = await SetSubmitAccount(submitAccount);
            if (result != null)
                goto end;

            var transaction = NewFriendTransaction(AccountId, ChainId, FriendRequestMode.Remove, friendId);

            result = await _client.SendDataTransaction(transaction, true);
            if (result.TransactionResult == TransactionResultTypes.Ok)
            {
                UIApp.Run(() => DownloadFriends(false));

                var chats = GetChats(friendId);
                foreach (var chat in chats)
                    _chats.Remove(chat.Index);

                await SaveAsync();
                await UIApp.PubSub.PublishAsync(new MessageNodeRefreshEvent(this));
            }

        end:

            var @event = new UnfriendEvent(friendId, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        public async Task<FriendsDownloadEvent> DownloadFriends(bool storedDataOnly)
        {
            var result = MessageNodeEventResultTypes.Unknown;

            if (!ServiceNode.HasUnlockedServiceAccount)
            {
                result = MessageNodeEventResultTypes.NoUnlockedAccount;
                goto end;
            }

            if (storedDataOnly)
            {
                if (_friendInfo == null)
                    result = MessageNodeEventResultTypes.DownloadFailed;
                else
                    result = MessageNodeEventResultTypes.StoredData;

                goto end;
            }

            var friendInfo = (await Transactions.Features.Friend.DownloadFriendInfo(_client, ChainType.Data, ChainId, MessageServiceInfo.FriendChainIndex, AccountId))?.Item;
            if (friendInfo != null)
            {
                var update = false;
                if (_friendInfo == null)
                {
                    _friendInfo = friendInfo;
                    update = true;
                }
                else
                {
                    if (friendInfo.LastTransactionInfo.TransactionId > _friendInfo.LastTransactionInfo.TransactionId)
                    {
                        _friendInfo = friendInfo;
                        update = true;
                    }
                }

                if (update)
                {
                    _ = QueryMissingProfiles();
                    // update

                    foreach (var friendId in _friendInfo.Friends)
                    {
                        if (!_friends.ContainsKey(friendId))
                            _friends.Add(friendId, new Friend(friendId, this));
                    }

                    var removed = new HashSet<long>();
                    foreach (var friend in _friends.Values)
                    {
                        if (!_friendInfo.Friends.Contains(friend.AccountId))
                            removed.Add(friend.AccountId);
                    }

                    foreach (var friendId in removed)
                        _friends.Remove(friendId);


                    await SaveAsync();
                    result = MessageNodeEventResultTypes.Ok;
                }
                else
                {
                    result = MessageNodeEventResultTypes.StoredData;
                }
            }
            else
            {
                result = MessageNodeEventResultTypes.DownloadFailed;
            }

        end:

            var @event = new FriendsDownloadEvent(_friendInfo, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        public async Task<InboxRecordDownloadEvent> DownloadInboxRecords(long accountId)
        {
            var items = new List<InboxNameRecordInfo>();
            var result = MessageNodeEventResultTypes.Unknown;

            if (!ServiceNode.HasUnlockedServiceAccount)
            {
                result = MessageNodeEventResultTypes.NoUnlockedAccount;
                goto end;
            }

            var next = (await _client.DownloadNextServiceAccountKeyIndex(accountId, ChainId)).Data;
            if (next == null || !next.IsValid)
            {
                result = MessageNodeEventResultTypes.DownloadFailed;
                goto end;
            }

            var keyCount = next.Item - 1;
            var indices = new List<Chain.Index>();

            for (short i = 0; i <= keyCount; i++)
                indices.Add(MessageServiceInfo.GetInboxIndex(i));

            var data = await AccountIndex.DownloadLastTransactionInfoIndicesBatch(_client, ChainType.Data, ChainId, MessageServiceInfo.MessageDataChainIndex, accountId, indices);
            if (data == null)
            {
                result = MessageNodeEventResultTypes.DownloadFailed;
                goto end;
            }

            if (data.ResultType != ResultTypes.Ok)
            {
                result = MessageNodeEventResultTypes.InvalidAccount;
                goto end;
            }

            var batch = data.Item;
            var count = batch.Count;
            for (short i = 0; i < count; i++)
            {
                InboxNameRecord record = null;
                (var success, _, var last) = batch.GetInfo(i);
                if (success)
                {
                    try
                    {
                        var transactionData = await ServiceNode.GetTransactionDownloadManager(MessageServiceInfo.MessageDataChainIndex).DownloadTransaction(last.TransactionId);
                        if (transactionData.Ok && transactionData.Count == 1 && transactionData.Transactions[0].Transaction.TryGetFeature<Data>(Data.FeatureId, out var featureData))
                        {
                            var recordData = featureData.Items[0].BinaryValue;
                            record = new InboxNameRecord(new Unpacker(recordData));
                        }
                    }
                    catch { }
                }

                items.Add(new InboxNameRecordInfo(i, record));
            }

            if (items.Count > 0)
            {
                result = MessageNodeEventResultTypes.Ok;
                if(accountId == AccountId)
                    InboxNameRecords = items;
            }
            else
                result = MessageNodeEventResultTypes.InvalidAccount;

            end:

            var @event = new InboxRecordDownloadEvent(accountId, items, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        public async Task<InboxRenameEvent> RenameInbox(short keyIndex, string title)
        {
            var submitAccount = ServiceNode.GetSubmitAccounts<SubmitAccount>(MessageServiceInfo.SubmitAccountIndex).FirstOrDefault();
            var result = await SetSubmitAccount(submitAccount);
            if (result != null)
                goto end;

            if (string.IsNullOrEmpty(title) || title.Length > MessageServiceInfo.MaxInboxNameLength)
            {
                result = new HeleusClientResponse(HeleusClientResultTypes.Ok, (long)ServiceUserCodes.InboxNameInvalid);
                goto end;
            }

            var info = new DataTransaction(AccountId, ChainId, MessageServiceInfo.MessageDataChainIndex);
            info.PrivacyType = DataTransactionPrivacyType.PublicData;

            info.EnableFeature<AccountIndex>(AccountIndex.FeatureId).Index = MessageServiceInfo.GetInboxIndex(keyIndex);
            info.EnableFeature<Data>(Data.FeatureId).AddBinary(MessageServiceInfo.MessageDataIndex, new InboxNameRecord(true, title).ToByteArray());

            result = await _client.SendDataTransaction(info, true);

            if (result.TransactionResult == TransactionResultTypes.Ok)
                UIApp.Run(() => DownloadInboxRecords(AccountId));

        end:

            var @event = new InboxRenameEvent(title, keyIndex, this, result);
            await UIApp.PubSub.PublishAsync(@event);
            return @event;
        }

        class PollResult
        {
            public readonly HashSet<Chain.Index> Indices = new HashSet<Chain.Index>();
            public bool UpdateFriends;
        }

        async Task<bool> Poll(TransactionDownload<Transaction> download, PollResult result)
        {
            var downloadResult = await download.DownloadTransactions();
            if (downloadResult.Ok)
            {
                foreach (var transactionDownload in downloadResult.Transactions)
                {
                    var transaction = transactionDownload.Transaction;
                    var type = (transaction as DataTransaction).TransactionType;

                    if (transaction.HasFeatureRequest(FriendRequest.FriendRequestId))
                    {
                        result.UpdateFriends = true;
                    }
                    else if (transaction.TryGetFeature<SharedAccountIndex>(SharedAccountIndex.FeatureId, out var sharedAccountIndex))
                    {
                        var index = sharedAccountIndex.Index;
                        if (MessageServiceInfo.IsValidConversationIndex(index))
                        {
                            result.Indices.Add(index);
                        }
                    }
                }

                return true;
            }

            return false;
        }

        // active polling is meh, should be replace by a simple notification server, but it's ok for now
        async Task PollLoop()
        {
            var profileInfo = (await ProfileManager.Current.GetProfileData(AccountId, ProfileDownloadType.ForceDownload, false)).ProfileInfo;
            if (profileInfo != null)
                ProfileInfo = profileInfo;

            await DownloadInboxRecords(AccountId);
            await QueryMissingProfiles();

            while (true)
            {
                try
                {
                    await DownloadFriends(false);

                    var save = false;
                    var lastAccountTransaction = (await PreviousAccountTransaction.DownloadLastTransactionInfo(_client, ChainType.Data, ChainId, MessageServiceInfo.MessageDataChainIndex, AccountId))?.Item;
                    var lastReceiverTransaction = (await Receiver.DownloadLastTransactionInfo(_client, ChainType.Data, ChainId, MessageServiceInfo.MessageDataChainIndex, AccountId))?.Item;

                    if (_lastAccountTransaction == null)
                    {
                        _lastAccountTransaction = lastAccountTransaction;
                        save = true;
                    }
                    if (_lastReceivedTransaction == null)
                    {
                        _lastReceivedTransaction = lastReceiverTransaction;
                        save = true;
                    }

                    var result = new PollResult();
                    if (lastAccountTransaction != null)
                    {
                        if (lastAccountTransaction.TransactionId > _lastAccountTransaction.TransactionId)
                        {
                            var download = new AccountTransactionDownload(AccountId, ServiceNode.GetTransactionDownloadManager(MessageServiceInfo.MessageDataChainIndex))
                            {
                                MinimalTransactionId = Math.Max(1, _lastAccountTransaction.TransactionId)
                            };

                            if (await Poll(download, result))
                            {
                                _lastAccountTransaction = lastAccountTransaction;
                                save = true;
                            }
                        }
                    }

                    if (lastReceiverTransaction != null)
                    {
                        if (lastReceiverTransaction.TransactionId > _lastReceivedTransaction.TransactionId)
                        {
                            var download = new ReceiverTransactionDownload(AccountId, ServiceNode.GetTransactionDownloadManager(MessageServiceInfo.MessageDataChainIndex))
                            {
                                MinimalTransactionId = Math.Max(1, _lastReceivedTransaction.TransactionId)
                            };

                            if (await Poll(download, result))
                            {
                                _lastReceivedTransaction = lastReceiverTransaction;
                                save = true;
                            }
                        }
                    }

                    if (result.UpdateFriends)
                        await DownloadFriends(false);

                    foreach (var index in result.Indices)
                    {
                        var chat = GetChat(index, true);
                        if(!IsNodeChat(chat))
                        {
                            _chats[chat.Index] = chat;

                            GenerateSubmitAccount(index);
                            await GenerateDefaultExchangeKeys();
                        }

                        await chat.DownloadMessages(false);
                    }

                    if (result.Indices.Count > 0)
                        await UIApp.PubSub.PublishAsync(new MessageNodeRefreshEvent(this));

                    if (save)
                        await SaveAsync();


                    await Task.Delay(5000);
                }
                catch(Exception ex)
                {
                    Log.HandleException(ex);
                }
            }
        }
    }
}
