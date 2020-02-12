using Heleus.Base;
using Heleus.MessageService;

namespace Heleus.Apps.Shared
{
    public class InboxNameRecordInfo : IPackable
    {
        public readonly short KeyIndex;
        public string Title => InboxRecord?.Title;
        public readonly InboxNameRecord InboxRecord;

        public InboxNameRecordInfo(short keyIndex, InboxNameRecord inboxRecord)
        {
            KeyIndex = keyIndex;
            InboxRecord = inboxRecord;
        }

        public InboxNameRecordInfo(Unpacker unpacker)
        {
            unpacker.Unpack(out KeyIndex);
            if (unpacker.UnpackBool())
                InboxRecord = new InboxNameRecord(unpacker);
        }

        public void Pack(Packer packer)
        {
            packer.Pack(KeyIndex);
            if (packer.Pack(InboxRecord != null))
                packer.Pack(InboxRecord);
        }
    }
}