using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.MessageService;

namespace Heleus.Apps.Shared
{
    public class InboxesPage : StackPage
    {
        protected readonly ServiceNodeButtonRow _serviceNodeButton;
        MessageNode _node => MessageApp.Current.GetNode(_serviceNodeButton.ServiceNode);

        public InboxesPage() : base("InboxesPage")
        {
            Subscribe<InboxRecordDownloadEvent>(InboxDownload);

            IsSuspendedLayout = true;

            AddTitleRow("Title");

            AddHeaderRow("Common.ServiceNode");
            _serviceNodeButton = AddRow(new ServiceNodeButtonRow(this, ServiceNodesPageSelectionFlags.ActiveRequired, "inboxes"));
            _serviceNodeButton.SelectionChanged = ServiceNodeChanged;
            AddInfoRow("Common.ServiceNodeInfo");
            AddFooterRow();

            IsBusy = true;

            UIApp.Run(Update);
        }

        Task InboxDownload(InboxRecordDownloadEvent arg)
        {
            if(arg.Node == _node && _node.AccountId == arg.AccountId)
            {
                AddInboxes(this, arg.Node.AccountId, Inbox, arg);
            }

            return Task.CompletedTask;
        }

        async Task Update()
        {
            var node = _node;
            if (node == null)
            {
                Toast("");
                return;
            }

            await node.DownloadInboxRecords(node.AccountId);

            IsBusy = false;
            UpdateSuspendedLayout();
        }

        void AddInboxes(StackPage page, long accountId, Func<ButtonRow, Task> action, InboxRecordDownloadEvent downloadResult)
        {
            page.RemoveHeaderSection("Inboxes");

            page.AddIndex = page.GetRow("Title");
            page.AddIndex = page.AddHeaderRow("Inboxes");

            var result = downloadResult;
            //if(result == null)
                //result = await node.DownloadInboxRecords(accountId);

            if (result.Result == MessageNodeEventResultTypes.Ok)
            {
                foreach (var item in result.InboxRecords)
                {
                    var button = page.AddButtonRow(null, action);
                    page.AddIndex = button;

                    var inboxName = item.InboxRecord?.Title;
                    if (inboxName == null)
                        inboxName = Tr.Get("Common.Inbox");

                    button.SetMultilineText(inboxName, Tr.Get("Common.InboxName", accountId, item.KeyIndex));
                    button.SetDetailViewIcon(Icons.Inbox);
                    button.Tag = item;
                }
            }
            else
            {
                if (result.Result == MessageNodeEventResultTypes.InvalidAccount)
                {
                    page.AddIndex = page.AddInfoRow("InvalidAccount");
                }
                else
                {
                    page.AddIndex = page.AddInfoRow("DownloadFailed");
                }
            }

            page.AddIndex = page.AddFooterRow();
        }

        async Task Inbox(ButtonRow arg)
        {
            var node = _node;
            if (node == null)
                return;

            var edit = T("EditName");
            var share = T("Share");
            var copy = T("Copy");
            var cancel = Tr.Get("Common.Cancel");

            var actions = new List<string> { edit, copy };

            if (UIApp.CanShare)
                actions.Add(share);

            var record = arg.Tag as InboxNameRecordInfo;
            var result = await DisplayActionSheet(Tr.Get("Common.Action"), cancel, null, actions.ToArray());
            if (result == edit)
            {
                await Navigation.PushAsync(new EditInboxPage(node, arg.Tag as InboxNameRecordInfo));
            }
            else if (result == copy)
            {
                var code = AppBase.Current.GetRequestCode(node.ServiceNode, MessageServiceInfo.MessageDataChainIndex, ViewFriendSchemeAction.ActionName, node.AccountId, record.KeyIndex);
                UIApp.CopyToClipboard(code);
                Toast("Copied");
            }
            else if (result == share)
            {
                var code = AppBase.Current.GetRequestCode(_node.ServiceNode, MessageServiceInfo.MessageDataChainIndex, ViewFriendSchemeAction.ActionName, _node.AccountId, record.KeyIndex);
                UIApp.Share(code);
            }
        }

        Task ServiceNodeChanged(ServiceNodeButtonRow obj)
        {
            IsBusy = true;
            UIApp.Run(Update);
            return Task.CompletedTask;
        }
    }
}
