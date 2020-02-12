using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.Base;
using Heleus.MessageService;
using Heleus.Network.Client;
using Heleus.Transactions;
using Xamarin.Forms;

namespace Heleus.Apps.Shared
{
    public class ChatListView : TransactionDownloadListView<ButtonRow>
    {
        readonly Chat _chat;

        public ChatListView(Chat inbox, StackPage page, View header) : base(inbox.Download, page, header)
        {
            _chat = inbox;
            page.Subscribe<NewSecretKeyEvent>(NewSecretKey);
        }

        Task NewSecretKey(NewSecretKeyEvent arg)
        {
            DecryptMessageRows();
            return Task.CompletedTask;
        }

        public static (string, string) GetMessageText(DecryptedRecordData<MessageRecord> message)
        {
            /*
            var accountId = message.Transaction.AccountId;

            string name = null;

            if (accountId == _chat.AccountId)
            {
                name = _chat.Node.ProfileInfo?.RealName;
            }
            else
            {
                var friend = _chat.Node.GetFriend(_chat.FriendAccountId);
                name = friend?.Profile?.RealName;
            }

            if (name == null)
                name = $"Account {accountId}";
            */

            var text = message.Record?.Text ?? "*";
            var state = message.DecryptetState;

            if (state == DecryptedDataRecordState.SecretKeyMissing)
                text = Tr.Get("Common.MessageSecretKeyMissing");
            else if (state == DecryptedDataRecordState.DecryptionError)
                text = Tr.Get("Common.MessageDecryptionError");

            var timestamp = message.Transaction.Timestamp;
            var detail = $"{(Time.PassedDays(timestamp) > 1 ? Time.DateTimeString(timestamp) : Time.TimeString(timestamp))}";

            return (text, detail);
        }

        protected override ButtonRow AddRow(StackPage page, TransactionDownloadData<Transaction> transaction)
        {
            var message = _chat.GetMessage(transaction);
            var accountId = transaction.Transaction.AccountId;

            var button = page.AddButtonRow(null, Message);
            button.LabelPadding = 0;
            button.RowStyle = Theme.MessageButton;

            button.RowLayout.Children.RemoveAt(0);
            button.RowLayout.Children.Remove(button.FontIcon);
            button.Label.Margin = new Thickness(5, 2, 5, 2);

            var (text, detail) = GetMessageText(message);
            button.SetMultilineText(text, detail);
            button.Tag = message;

            if (accountId == _chat.AccountId)
            {
                button.Margin = new Thickness(0, 0, 46, 0);
            }
            else
            {
                button.Margin = new Thickness(46, 0, 0, 0);
            }

            return button;
        }

        async Task Message(ButtonRow arg)
        {
            var message = arg.Tag as DecryptedRecordData<MessageRecord>;
            await _page.Navigation.PushAsync(new MessagePage(_chat, message));
        }

        protected override long GetTransactionId(ButtonRow row)
        {
            var message = row.Tag as DecryptedRecordData<MessageRecord>;

            return message.Transaction.TransactionId;
        }

        protected override async Task More(ButtonRow button)
        {
            _page.IsBusy = true;
            await _chat.DownloadMessages(true);
        }

        public void DecryptMessageRows()
        {
            UIApp.Run(async () =>
            {
                var rows = Rows;
                foreach (var row in rows)
                {
                    var message = row.Tag as DecryptedRecordData<MessageRecord>;

                    if (message.Record == null)
                        await message.Decrypt();

                    var (text, detail) = GetMessageText(message);

                    var invalid = message.DecryptetState != DecryptedDataRecordState.Decrypted;
                    if (invalid)
                    {
                        row.Label.FormattedText.Spans[0].SetColorStyle(Theme.ErrorColor);
                    }
                    else
                    {
                        row.Label.FormattedText.Spans[0].SetColorStyle(Theme.TextColor);
                    }

                    row.Label.FormattedText.Spans[0].Text = text;
                    row.Label.FormattedText.Spans[1].Text = $"\n{detail}";
                }

                _page.UpdateSuspendedLayout();
            });
        }
    }
}
