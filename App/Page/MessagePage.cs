using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.MessageService;

namespace Heleus.Apps.Shared
{
    public class MessagePage : StackPage
    {
        readonly Chat _chat;
        readonly DecryptedRecordData<MessageRecord> _message;
        readonly SecretKeyView _keyView;
        readonly LabelRow _text;

        public MessagePage(Chat chat, DecryptedRecordData<MessageRecord> message) : base("MessagePage")
        {
            Subscribe<NewSecretKeyEvent>(NewSecretKey);

            _chat = chat;
            _message = message;

            var transaction = message.Transaction;

            AddTitleRow("Title");

            AddHeaderRow("Message");

            var (text, detail) = ChatListView.GetMessageText(message);
            _text = AddTextRow(null);
            _text.SetMultilineText(text, detail);

            AddFooterRow();

            AddHeaderRow("SecretKeyInfo");
            _keyView = new SecretKeyView(message.EncryptedRecord?.KeyInfo, true);
            AddViewRow(_keyView);
            AddButtonRow("Import", Import);
            AddFooterRow();

            AddHeaderRow("TransactionInfo");
            AddViewRow(new DataTransactionView(transaction));
            AddFooterRow();

            IsBusy = true;
            UIApp.Run(Update);
        }

        async Task Update()
        {
            await _message.Decrypt();

            var (text, detail) = ChatListView.GetMessageText(_message);
            _text.Label.FormattedText.Spans[0].Text = text;
            _text.Label.FormattedText.Spans[1].Text = $"\n{detail}";

            IsBusy = false;
        }

        async Task NewSecretKey(NewSecretKeyEvent arg)
        {
            await Update();
        }

        async Task Import(ButtonRow arg)
        {
            var serviceNode = _chat.Node.ServiceNode;

            var account = serviceNode.GetSubmitAccount<MessageSubmitAccount>(_chat.KeyIndex, _chat.Index);
            if (account != null)
                await Navigation.PushAsync(new SecretKeysPage(account));
        }
    }
}
