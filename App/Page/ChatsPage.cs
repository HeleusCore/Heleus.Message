using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;

namespace Heleus.Apps.Shared
{
    public class ChatsPage : StackPage
    {
        ChatsListView _listView;

        public ChatsPage() : base("ChatsPage")
        {
            Subscribe<ServiceAccountAuthorizedEvent>(AccountAuth);
            Subscribe<ServiceAccountImportEvent>(AccountImport);
            Subscribe<ServiceNodesLoadedEvent>(NodesLoaded);
            Subscribe<MessageNodeRefreshEvent>(NodeRefresh);

            IsSuspendedLayout = true;

            SetupPage();
        }

        void UpdateChats()
        {
            IsBusy = false;

            var chats = MessageApp.Current.GetAllChats();
            if (chats.Count == 0)
            {
                ShowInfo();
                UpdateSuspendedLayout();
                return;
            }

            RemoveView(GetRow("Info"));

            if (!UIAppSettings.AppReady)
            {
                UIAppSettings.AppReady = true;
                UIApp.Current.SaveSettings();
            }

            var header = GetRow<HeaderRow>("Chats");
            if (header == null)
            {
                header = AddHeaderRow();
                header.Identifier = "Chats";
                _listView = new ChatsListView(this, header);
                AddFooterRow();
            }

            _listView.Update(chats);
            if(_listView.Rows.Count == 0)
            {
                RemoveHeaderSection("Chats");
                ShowInfo();
            }

            UpdateSuspendedLayout();
        }

        Task NodeRefresh(MessageNodeRefreshEvent arg)
        {
            UpdateChats();
            return Task.CompletedTask;
        }

        Task NodesLoaded(ServiceNodesLoadedEvent arg)
        {
            UpdateChats();
            return Task.CompletedTask;
        }

        void ShowInfo()
        {
            if (!ServiceNodeManager.Current.HadUnlockedServiceNode)
                return;

            if (GetRow("Info") == null)
                AddInfoRow("Info");
        }

        void SetupPage()
        {
            StackLayout.Children.Clear();

            AddTitleRow("Title");

            if (!ServiceNodeManager.Current.HadUnlockedServiceNode)
            {
                AddInfoRow("Auth", Tr.Get("App.FullName"));

                ServiceNodesPage.AddAuthorizeSection(ServiceNodeManager.Current.NewDefaultServiceNode, this, false);
            }
            else
            {
                if (UIAppSettings.AppReady)
                {
                    IsBusy = true;
                    if (ServiceNodeManager.Current.Ready)
                        UpdateChats();
                }
                else
                    ShowInfo();
            }

            UpdateSuspendedLayout();
        }

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
    }
}
