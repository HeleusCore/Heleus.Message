using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.Chain.Data;
using Heleus.MessageService;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Shared
{
    public class ContactsPage : StackPage
    {
        ServiceNodeButtonRow _serviceNodeButton;
        MessageNode _node => MessageApp.Current.GetNode(_serviceNodeButton?.ServiceNode);

        FriendInfo _friendInfo;
        HeaderRow _footer;
        ContactsListView _listView;

        Task AccountImport(ServiceAccountImportEvent arg)
        {
            SetupPage();

            return Task.CompletedTask;
        }

        Task AccountAuth(ServiceAccountAuthorizedEvent arg)
        {
            SetupPage();

            return Task.CompletedTask;
        }

        Task FriendsDownload(FriendsDownloadEvent arg)
        {
            if (arg.Node != _node)
                return Task.CompletedTask;

            if (arg.FriendInfo != null)
                _friendInfo = arg.FriendInfo;

            UpdateFriends(arg.Node);
            return Task.CompletedTask;
        }

        public ContactsPage() : base("ContactsPage")
        {
            Subscribe<FriendsDownloadEvent>(FriendsDownload);
            Subscribe<ServiceAccountAuthorizedEvent>(AccountAuth);
            Subscribe<ServiceAccountImportEvent>(AccountImport);
            Subscribe<ServiceNodesLoadedEvent>(Loaded);

            IsSuspendedLayout = true;

            SetupPage();
        }

        Task Loaded(ServiceNodesLoadedEvent arg)
        {
            if(_serviceNodeButton != null && _serviceNodeButton.ServiceNode == null)
            {
                _serviceNodeButton.ServiceNode = AppBase.Current.GetLastUsedServiceNode("contacts");
            }

            return Task.CompletedTask;
        }

        public override void OnOpen()
        {
            IsBusy = true;
            UIApp.Run(Update);
        }

        void SetupPage()
        {
            StackLayout.Children.Clear();

            _footer = AddTitleRow("Title");
            _serviceNodeButton = null;

            if (!ServiceNodeManager.Current.HadUnlockedServiceNode)
            {
                AddHeaderRow("Auth");
                AddButtonRow("Authorize", Authorize);
                AddInfoRow("AutorhizeInfo");
                AddFooterRow();
            }
            else
            {
                AddHeaderRow("MiscHeader");

                var button = AddButtonRow("Search", Search);
                button.SetDetailViewIcon(Icons.Search);

                button = AddButtonRow("Pending", Pending);
                button.SetDetailViewIcon(Icons.UserClock);

                button = AddButtonRow("YourContact", YourContact);
                button.SetDetailViewIcon(Icons.User);

                if (UIApp.CanShare)
                {
                    button = AddButtonRow("Share", Share);
                    button.SetDetailViewIcon(Icons.Share);
                }
                button = AddButtonRow("Copy", Copy);
                button.SetDetailViewIcon(Icons.Copy);

                AddInfoRow("MiscInfo");

                AddFooterRow();

                AddHeaderRow("Common.ServiceNode");
                _serviceNodeButton = AddRow(new ServiceNodeButtonRow(this, ServiceNodesPageSelectionFlags.ActiveRequired | ServiceNodesPageSelectionFlags.UnlockedAccountRequired, "contacts"));
                _serviceNodeButton.SelectionChanged = ServiceNodeChanged;
                AddInfoRow("Common.ServiceNodeInfo");
                AddFooterRow();
            }
        }

        async Task YourContact(ButtonRow arg)
        {
            await Navigation.PushAsync(new ContactPage(_node, _node.AccountId));
        }

        Task Copy(ButtonRow arg)
        {
            var code = AppBase.Current.GetRequestCode(_node.ServiceNode, MessageServiceInfo.MessageDataChainIndex, ViewFriendSchemeAction.ActionName, _node.AccountId);
            UIApp.CopyToClipboard(code);
            Toast("Copied");
            return Task.CompletedTask;
        }

        Task Share(ButtonRow arg)
        {
            var code = AppBase.Current.GetRequestCode(_node.ServiceNode, MessageServiceInfo.MessageDataChainIndex, ViewFriendSchemeAction.ActionName, _node.AccountId);
            UIApp.Share(code);
            return Task.CompletedTask;
        }

        async Task Authorize(ButtonRow button)
        {
            await UIApp.Current.ShowPage(typeof(ChatsPage));
        }

        async Task Update()
        {
            var node = _node;
            if (node != null)
            {
                await node.DownloadFriends(true);
                await node.DownloadFriends(false);
            }

            AddIndex = null;
            UpdateSuspendedLayout();

            IsBusy = false;
        }

        void UpdateFriends(MessageNode node)
        {
            if (!ServiceNodeManager.Current.HadUnlockedServiceNode)
                return;

            AddIndex = GetRow("Friends");
            if (AddIndex == null)
            {
                AddIndex = _footer;
                AddIndex = AddHeaderRow("Friends");

                AddFooterRow();
            }

            RemoveView(GetRow("NoFriends"));
            var rows = GetHeaderSectionRows("Friends");
            if (rows.Count == 0 && (_friendInfo == null || _friendInfo.Friends.Count == 0))
            {
                AddInfoRow("NoFriends");
                _listView = null;
            }
            else
            {
                if (_friendInfo != null)
                {
                    if (_listView == null || _listView.Node != node)
                    {
                        _listView = new ContactsListView(node, this, (HeaderRow)AddIndex);
                    }
                    _listView.Update(_friendInfo.Friends);
                }
            }
        }

        async Task Search(ButtonRow arg)
        {
            var node = _node;
            if (node != null)
                await Navigation.PushAsync(new SearchContactProfilePage(node));
        }

        async Task Pending(ButtonRow arg)
        {
            var node = _node;
            if (node != null)
                await Navigation.PushAsync(new PendingContactsPage(node, _friendInfo));
        }

        Task ServiceNodeChanged(ServiceNodeButtonRow obj)
        {
            IsBusy = true;
            RemoveHeaderSection("Friends");
            UIApp.Run(Update);

            return Task.CompletedTask;
        }
    }
}
