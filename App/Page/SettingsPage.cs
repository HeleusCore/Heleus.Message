using System;
using System.Threading.Tasks;

namespace Heleus.Apps.Shared
{
    class SettingsPage : SettingsPageBase
    {
        public SettingsPage()
        {
            IsSuspendedLayout = true;
            UIApp.Run(Update);
        }

        Task Update()
        {
            AddTitleRow("Title");

            AddHeaderRow().Label.Text = Tr.Get("App.FullName");

            AddButtonRow("InboxesPage.Inboxes", Inboxes).SetDetailViewIcon(Icons.Inbox);

            AddButtonRow(ServiceNodesPage.PageTitle, async (button) =>
            {
                if (!ServiceNodeManager.Current.Ready)
                {
                    await MessageAsync("ServiceNodeManagerNotReady");
                    return;
                }

                await Navigation.PushAsync(new ServiceNodesPage());
            }).SetDetailViewIcon(ServiceNodesPage.PageIcon);

            AddButtonRow(HandleRequestPage.HandleRequestTranslation, async (button) =>
            {
                await Navigation.PushAsync(new HandleRequestPage());
            }).SetDetailViewIcon(HandleRequestPage.HandleRequestIcon);

            AddButtonRow("About", async (button) =>
            {
                await Navigation.PushAsync(new AboutPage());
            }).SetDetailViewIcon(Icons.Info);

            AddFooterRow();

            AddAppInfoSection();

            AddPushNotificationSection();

            AddThemeSection();
            //AddNotificationSection();
#if DEBUG
            AddButtonRow("Icons", async (button) =>
            {
                await Navigation.PushAsync(new IconsPage());
            });
#endif
            UpdateSuspendedLayout();
            return Task.CompletedTask;
        }

        async Task Inboxes(ButtonRow arg)
        {
            if (!ServiceNodeManager.Current.Ready)
            {
                await MessageAsync("ServiceNodeManagerNotReady");
                return;
            }

            if(ServiceNodeManager.Current.FirstUnlockedServiceNode == null)
            {
                await MessageAsync("NoUnlockedServiceNode");
                return;
            }

            await Navigation.PushAsync(new InboxesPage());
        }
    }
}
