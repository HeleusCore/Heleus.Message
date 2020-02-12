using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.Transactions;
using Xamarin.Forms;

namespace Heleus.Apps.Shared
{

    public class ChatPage : StackPage
    {
        readonly MessageNode _node;
        readonly Chat _chat;
        readonly EntryRow _text;
        readonly ChatListView _listView;
        readonly ButtonRow _submit;

        public ChatPage(Chat chat) : base("ChatPage")
        {
            Subscribe<MessageSentEvent>(MessageSent);
            Subscribe<ChatMessageDownloadEvent>(MessagesDownloaded);

            IsSuspendedLayout = true;

            _node = chat.Node;
            _chat = chat;

            var title = AddTitleRow("Title");

            var friend = chat.Node.GetFriend(chat.FriendAccountId);
            var name = friend?.Profile?.RealName;

            if (name == null)
                name = $"Account {chat.FriendAccountId}";

            title.Label.Text = name;
            SetTitle(name);

            _text = new EntryRow(Icons.StickyNote);
            _text.Edit.Placeholder = T("TypeText");
            _text.SetDetailViewIcon(Icons.Pencil);

            _text.RowLayout.Children.Remove(_text.FontIcon);
            _text.RowLayout.Children.Remove(_text.Label);

            _text.VerticalOptions = LayoutOptions.FillAndExpand;
            _text.HorizontalOptions = LayoutOptions.FillAndExpand;

            _submit = new ButtonRow(Icons.RowSubmit, Submit);
            _submit.RowStyle = Theme.SubmitButton;
            _submit.FontIcon.Margin = new Thickness(0, 0, 0, 0);
            _submit.WidthRequest = _submit.HeightRequest = 40;

            AbsoluteLayout.SetLayoutBounds(_submit.FontIcon, new Rectangle(0.5, 0.5, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

            Status.AddBusyView(_submit);
            Status.AddBusyView(_text);

            _text.Edit.TextChanged += Edit_TextChanged;
            Edit_TextChanged(_text.Edit, null);

            var layout = new StackLayout();
            layout.Orientation = StackOrientation.Horizontal;

            layout.Children.Add(_text);
            layout.Children.Add(_submit);

            AddView(layout);

            _listView = new ChatListView(chat, this, layout);

            ToolbarItems.Add(new ExtToolbarItem(T("Info"), null, Info));

            IsBusy = true;
            UIApp.Run(Update);
        }

        async Task Info()
        {
            await Navigation.PushAsync(new ContactPage(_node, _chat));
        }

        void Edit_TextChanged(object sender, TextChangedEventArgs e)
        {
            _submit.IsEnabled = !string.IsNullOrWhiteSpace(_text.Edit.Text);
        }

        async Task MessagesDownloaded(ChatMessageDownloadEvent arg)
        {
            if (arg.Chat == _chat)
            {
                IsBusy = false;
                Edit_TextChanged(null, null);

                _listView.UpdateTransactions();
                UpdateSuspendedLayout();

                _listView.DecryptMessageRows();

                if (_chat.UpdateLastViewedCount())
                {
                    await _chat.Node.SaveAsync();
                }
            }
        }

        async Task MessageSent(MessageSentEvent arg)
        {
            var result = arg.Result;
            if(result.TransactionResult != TransactionResultTypes.Ok)
            {
                IsBusy = false;

                await ErrorTextAsync(result.GetErrorMessage());
                return;
            }

            _text.Edit.Text = string.Empty;
            Edit_TextChanged(_text.Edit, null);
        }

        Task Submit(ButtonRow arg)
        {
            var text = _text.Edit.Text;

            if (!string.IsNullOrEmpty(text))
            {
                IsBusy = true;

                UIApp.Run(() => _node.SendMessage(_chat, text));
            }

            return Task.CompletedTask;
        }

        async Task Update()
        {
            await _chat.Node.GenerateSubmitAccountAndExchangeKey(_chat);

            await _chat.QueryStoredMessages();
            await _chat.DownloadMessages(false);
            IsBusy = false;
            Edit_TextChanged(null, null);
        }
    }
}
