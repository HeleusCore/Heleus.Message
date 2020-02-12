using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Base;
using Heleus.Chain;
using Heleus.Cryptography;
using Heleus.Network.Results;
using Heleus.ProfileService;

namespace Heleus.Apps.Message
{
    public class Friend : IPackable, IUnpackerKey<long>
    {
        public readonly long AccountId;
        public long UnpackerKey => AccountId;
        public ProfileInfo Profile;

        readonly Dictionary<short, PublicServiceAccountKey> _keys = new Dictionary<short, PublicServiceAccountKey>();
        readonly MessageNode _node;

        public Friend(long accountId, MessageNode node)
        {
            AccountId = accountId;
            _node = node;
        }

        public Friend(Unpacker unpacker, MessageNode node)
        {
            var chainId = node.ChainId;

            unpacker.Unpack(out AccountId);
            unpacker.Unpack(_keys, (u) =>
            {
                var key = new PublicServiceAccountKey(AccountId, chainId, u);
                return (key.KeyIndex, key);
            });

            if (unpacker.UnpackBool())
                Profile = new ProfileInfo(unpacker);

            _node = node;
        }

        public void Pack(Packer packer)
        {
            packer.Pack(AccountId);
            packer.Pack(_keys);

            if (packer.Pack(Profile != null))
                packer.Pack(Profile);
        }

        public async Task<PublicServiceAccountKey> GetSignedPublicKey(short keyIndex)
        {
            if (_keys.TryGetValue(keyIndex, out var key))
                return key;

            var account = (await _node.ServiceNode.Client.DownloadValidServiceAccountKey(AccountId, _node.ChainId, keyIndex)).Data;
            if (account != null && account.ResultType == ResultTypes.Ok)
            {
                key = account.Item;
                _keys[key.KeyIndex] = key;
            }

            return key;
        }
    }
}
