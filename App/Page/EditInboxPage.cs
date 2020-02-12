using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.MessageService;
using Heleus.Transactions;

namespace Heleus.Apps.Shared
{
    public class EditInboxPage : StackPage
    {
        readonly MessageNode _node;
        readonly InboxNameRecordInfo _inboxItem;
        readonly EntryRow _titleRow;

        string _title;

        async Task InboxRenamed(InboxRenameEvent arg)
        {
            IsBusy = false;

            var result = arg.Result;
            if (result.TransactionResult == TransactionResultTypes.Ok)
            {
                _title = arg.Title;
                Status.ReValidate();
                await MessageAsync("RenameSuccess");
            }
            else
            {
                await ErrorTextAsync(result.GetErrorMessage());
            }
        }

        async Task Rename(ButtonRow arg)
        {
            IsBusy = true;

            if (await ConfirmAsync("RenameConfirm"))
                UIApp.Run(() => _node.RenameInbox(_inboxItem.KeyIndex, _titleRow.Edit.Text));
        }

        public EditInboxPage(MessageNode node, InboxNameRecordInfo inboxItem) : base("EditInboxPage")
        {
            Subscribe<InboxRenameEvent>(InboxRenamed);

            _node = node;
            _inboxItem = inboxItem;
            _title = inboxItem.Title;

            AddTitleRow("Title");

            AddHeaderRow("RenameHeader");

            _titleRow = AddEntryRow(_title, "RenameEntry");

            Status.Add(_titleRow.Edit, T("RenameStatus"), (sv, edit, newText, oldText) =>
            {
                if (!string.IsNullOrEmpty(newText) && newText.Length <= MessageServiceInfo.MaxInboxNameLength && newText != _title)
                    return true;

                return false;
            });

            AddSubmitRow("RenameButton", Rename);

            AddFooterRow();
        }
    }
}
