using System.Collections.Generic;
using Heleus.Apps.Shared;

namespace Heleus.Apps.Message
{
    public class InboxRecordDownloadEvent : MessageNodeResultEvent
    {
        public readonly long AccountId;
        public readonly IReadOnlyList<InboxNameRecordInfo> InboxRecords;

        public InboxRecordDownloadEvent(long accountId, IReadOnlyList<InboxNameRecordInfo> inboxRecords, MessageNode node, MessageNodeEventResultTypes resultTypes) : base(node, resultTypes)
        {
            AccountId = accountId;
            InboxRecords = inboxRecords ?? new List<InboxNameRecordInfo>();
        }
    }
}
