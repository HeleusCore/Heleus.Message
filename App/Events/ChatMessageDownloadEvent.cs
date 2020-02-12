namespace Heleus.Apps.Message
{
    public class ChatMessageDownloadEvent
    {
        public readonly MessageNode Node;
        public readonly Chat Chat;

        public ChatMessageDownloadEvent(Chat inbox, MessageNode node)
        {
            Node = node;
            Chat = inbox;
        }
    }
}
