namespace Heleus.Apps.Message
{
    public enum MessageNodeEventResultTypes
    {
        Ok,
        StoredData,
        Unknown,

        NoUnlockedAccount,
        DownloadFailed,
        InvalidAccount
    }

    public class MessageNodeResultEvent
    {
        public readonly MessageNode Node;
        public readonly MessageNodeEventResultTypes Result;

        public MessageNodeResultEvent(MessageNode node, MessageNodeEventResultTypes resultTypes)
        {
            Node = node;
            Result = resultTypes;
        }
    }
}
