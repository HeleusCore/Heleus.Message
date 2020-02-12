using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.Base;
using Heleus.Chain.Data;
using Heleus.MessageService;
using Heleus.Transactions;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Shared
{
    public class ContactPage : StackPage
    {
        readonly long _friendAccountId;
        readonly short _friendKeyIndex;

        readonly MessageNode _node;
        bool _self => _node.AccountId == _friendAccountId;
        readonly Chat _chat;

        Task ProfileData(ProfileDataResultEvent arg)
        {
            if (arg.AccountId == _friendAccountId)
            {
                var profileData = arg.ProfileData;
                if (profileData.ProfileInfoResult == ProfileDownloadResult.Available)
                {
                    if (ProfilePageSections.HasProfileSections(this))
                    {
                        if (ProfilePageSections.UpdateProfileSections(this, arg.ProfileData))
                            UpdateSuspendedLayout();
                    }
                    else
                    {
                        AddIndex = GetRow("Title");
                        ProfilePageSections.AddProfileSections(this, arg.ProfileData, "Profile", true);
                        UpdateSuspendedLayout();
                    }
                }
            }

            return Task.CompletedTask;
        }


        async Task Unfriend(UnfriendEvent arg)
        {
            IsBusy = false;

            var result = arg.Result;

            if(result.TransactionResult == TransactionResultTypes.Ok)
            {
                await MessageAsync("UnfriendSuccess");
                await Navigation.PopAsync();
            }
            else
            {
                await ErrorTextAsync(result.GetErrorMessage());
            }
        }

        public ContactPage(MessageNode node, long friendAccountId, short keyIndex = -1, Chat chat = null) : base("ContactPage")
        {
            Subscribe<ProfileDataResultEvent>(ProfileData);
            Subscribe<UnfriendEvent>(Unfriend);

            _node = node;
            _chat = chat;
            _friendAccountId = friendAccountId;
            _friendKeyIndex = keyIndex;

            AddTitleRow("Title");

            if (!_self)
            {

                if (_chat == null)
                {
                    var friend = node.GetFriend(friendAccountId);
                    var invitation = node.GetInvitation(friendAccountId);

                    if (invitation != null)
                    {
                        AddHeaderRow("PendingInvitation");

                        if (invitation.HasFriendAccountApproval)
                        {
                            var row = AddButtonRow("ApproveButton", Approve);
                            row.SetDetailViewIcon(Icons.UserPlus);
                            row.Tag = invitation;
                        }

                        if (invitation.HasAccountApproval)
                        {
                            var row = AddButtonRow("AwaitingApprovalButton", Awaiting);
                            row.SetDetailViewIcon(Icons.UserClock);
                            row.Tag = invitation;
                        }

                        AddInfoRow("PendingInvitationInfo");
                        AddFooterRow();
                    }

                    if (friend == null && invitation == null)
                    {
                        AddHeaderRow("Invitation");

                        var b = AddButtonRow("InviteButton", Invite);
                        b.SetDetailViewIcon(Icons.UserPlus);
                        AddInfoRow("InvitationInfo");

                        AddFooterRow();
                    }

                    var chats = _node.GetChats(_friendAccountId);
                    if (chats.Count > 0)
                    {
                        AddHeaderRow("Chats");

                        foreach (var c in chats)
                        {
                            var b = AddButtonRow(Time.DateTimeString(c.LastTimestamp), OpenChat);
                            b.SetDetailViewIcon(Icons.Comments);
                            b.Tag = c;
                        }

                        AddFooterRow();
                    }

                    if (friend != null)
                    {
                        AddHeaderRow("Misc");

                        var button = AddButtonRow("Unfriend", Unfriend);
                        button.RowStyle = Theme.CancelButton;
                        button.SetDetailViewIcon(Icons.UserSlash);

                        AddFooterRow();
                    }
                }
                else
                {
                    AddHeaderRow("NofificationHeader");

                    var _notification = AddSwitchRow("Notification");
                    _notification.Switch.IsToggled = UIApp.Current.IsPushChannelSubscribed(_chat.Index);
                    _notification.Switch.ToggledAsync = Notification_Toggled;
                    _notification.SetDetailViewIcon(Icons.Bell);

                    AddFooterRow();


                    AddHeaderRow("Misc");

                    var button = AddButtonRow("ViewInboxes", ViewInboxes);
                    button.SetDetailViewIcon(Icons.Inbox);

                    button = AddButtonRow("ManageSecretKeys", ManageSecretKeys);
                    button.SetDetailViewIcon(Icons.Key);

                    button = AddButtonRow("RemoveChat", RemoveChat);
                    button.SetDetailViewIcon(Icons.CommentSlash);

                    button = AddButtonRow("Unfriend", Unfriend);
                    button.RowStyle = Theme.CancelButton;
                    button.SetDetailViewIcon(Icons.UserSlash);

                    AddFooterRow();
                }
            }
            else
            {
                AddHeaderRow("Misc");

                AddButtonRow("InboxesPage.Inboxes", Inboxes).SetDetailViewIcon(Icons.Inbox);

                if (UIApp.CanShare)
                {
                    var b = AddButtonRow("Share", Share);
                    b.SetDetailViewIcon(Icons.Share);
                }
                var button = AddButtonRow("Copy", Copy);
                button.SetDetailViewIcon(Icons.Copy);

                AddFooterRow();
            }

            IsBusy = true;
            UIApp.Run(Update);
        }

        async Task ManageSecretKeys(ButtonRow arg)
        {
            var serviceNode = _chat.Node.ServiceNode;

            var account = serviceNode.GetSubmitAccount<MessageSubmitAccount>(_chat.KeyIndex, _chat.Index);
            if (account != null)
                await Navigation.PushAsync(new SecretKeysPage(account));
        }

        public ContactPage(MessageNode node, Chat chat) : this(node, chat.FriendAccountId, chat.KeyIndex, chat)
        {
        }

        async Task Update()
        {
            await ProfileManager.Current.GetProfileData(_friendAccountId, ProfileDownloadType.DownloadIfNotAvailable, true);

            if (_chat == null)
            {
                AddIndex = GetRow("Misc");
                AddIndexBefore = true;
                AddIndex = AddHeaderRow("SelectInbox");
                AddIndexBefore = false;

                var result = await _node.DownloadInboxRecords(_friendAccountId);
                if (result.Result == MessageNodeEventResultTypes.Ok)
                {
                    SelectionItem<InboxNameRecordInfo> keyIndexItem = null;
                    var list = new SelectionItemList<InboxNameRecordInfo>();

                    foreach (var item in result.InboxRecords)
                    {
                        var selectionItem = new SelectionItem<InboxNameRecordInfo>(item, null);
                        list.Add(selectionItem);
                        if (item.KeyIndex == _friendKeyIndex)
                            keyIndexItem = selectionItem;
                    }

                    if (keyIndexItem != null)
                    {
                        list.Clear();
                        list.Add(keyIndexItem);
                    }

                    var row = AddSelectionRows(list, list[0].Key);
                    row.SelectionChanged = InboxChanged;

                    foreach (var button in row.Buttons)
                    {
                        var item = button.Tag as SelectionItem<InboxNameRecordInfo>;

                        var inboxName = item.Key.InboxRecord?.Title;
                        if (inboxName == null)
                            inboxName = Tr.Get("Common.Inbox");

                        button.SetMultilineText(inboxName, Tr.Get("Common.InboxName", _friendAccountId, item.Key.KeyIndex));
                        button.SetDetailViewIcon(Icons.Inbox);
                    }

                    AddIndex = row.Buttons[row.Buttons.Count - 1];
                    AddIndex = AddInfoRow("SelectInboxInfo");
                    var f = AddFooterRow();
                    f.Identifier = "SelectInboxFooter";

                    var friend = _node.GetFriend(_friendAccountId);
                    if(friend != null)
                        await InboxChanged(list[0].Key);
                }
                else
                {
                    if (result.Result == MessageNodeEventResultTypes.InvalidAccount)
                    {
                        AddIndex = AddInfoRow("InvalidAccount");
                    }
                    else
                    {
                        AddIndex = AddInfoRow("DownloadFailed");
                    }

                    var f = AddFooterRow();
                    f.Identifier = "SelectInboxFooter";
                }
            }

            IsBusy = false;
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

        async Task InboxChanged(InboxNameRecordInfo info)
        {
            var keyIndex = info.KeyIndex;
            var friend = _node.GetFriend(_friendAccountId);

            if (friend == null)
            {
                await ErrorAsync("NotFriends");
                return;
            }

            var success = _node.GenerateSubmitAccounts(_friendAccountId, keyIndex);

            RemoveHeaderSection("Inbox");
            AddIndex = GetRow("SelectInboxFooter");
            AddIndex = AddHeaderRow("Inbox");

            var row = AddRow(new SubmitAccountButtonRow<MessageSubmitAccount>(this, () => _node.ServiceNode.GetSubmitAccounts<MessageSubmitAccount>((su) => su.FriendAccountId == _friendAccountId && su.FriendKeyIndex == keyIndex), $"friend-{_friendAccountId}-{keyIndex}"));
            AddIndex = row;

            row.SelectionChanged = InboxChanged;
            row.Tag = info;

            AddIndex = AddInfoRow("InboxInfo");

            AddFooterRow();

            await InboxChanged(row);
        }

        Task InboxChanged(SubmitAccountButtonRow<MessageSubmitAccount> obj)
        {
            var submitAccount = obj.SubmitAccount;
            if (submitAccount == null)
                return Task.CompletedTask;

            var info = obj.Tag as InboxNameRecordInfo;
            if (info == null)
                return Task.CompletedTask;

            IsBusy = true;

            var index = MessageService.MessageServiceInfo.GetConversationIndex(submitAccount.AccountId, submitAccount.KeyIndex, submitAccount.FriendAccountId, submitAccount.FriendKeyIndex);
            var chat = _node.GetChat(index, true);

            AddIndex = GetRow("InboxInfo");
            AddIndexBefore = true;
            RemoveView(GetRow("SendMessage"));
            var b = AddButtonRow("SendMessage", SendMessage);
            b.SetDetailViewIcon(Icons.Pencil);
            b.Tag = chat;

            AddIndexBefore = false;

            IsBusy = false;
            return Task.CompletedTask;
        }

        async Task SendMessage(ButtonRow arg)
        {
            var chat = arg.Tag as Chat;
            if (chat == null)
                return;

            await Navigation.PushAsync(new ChatPage(chat));
        }

        async Task OpenChat(ButtonRow arg)
        {
            var chat = arg.Tag as Chat;
            await Navigation.PushAsync(new ChatPage(chat));
        }

        async Task ViewInboxes(ButtonRow arg)
        {
            await Navigation.PushAsync(new ContactPage(_node, _friendAccountId));
        }

        async Task Inboxes(ButtonRow arg)
        {
            await Navigation.PushAsync(new InboxesPage());
        }

        async Task RemoveChat(ButtonRow arg)
        {
            if(await ConfirmAsync("ConfirmRemoveChat"))
            {
                if(await _node.RemoveChat(_chat))
                {
                    await MessageAsync("RemoveChatSuccess");
                }
            }
        }

        async Task Unfriend(ButtonRow arg)
        {
            if(await ConfirmAsync("ConfirmUnfriend"))
            {
                IsBusy = true;
                UIApp.Run(() => _node.Unfriend(_friendAccountId));
            }
        }

        async Task Notification_Toggled(ExtSwitch @switch)
        {
            var notify = @switch.IsToggled;
            if (await ConfirmAsync(notify ? "EnableNotify" : "DisableNotify"))
            {
                IsBusy = true;
                if (await UIApp.Current.ChangePushChannelSubscription(this, _chat.Index))
                {
                    await MessageAsync("NotifyChanged");
                }
                @switch.SetToogle(UIApp.Current.IsPushChannelSubscribed(_chat.Index));

                IsBusy = false;
            }
        }

        async Task Awaiting(ButtonRow arg)
        {
            await MessageAsync("AwaitingApproval");
        }

        async Task Invite(ButtonRow arg)
        {
            if (await ConfirmAsync("InvitationConfirm"))
            {
                IsBusy = true;
                var result = (await _node.SendFriendInvitation(_friendAccountId)).Result;
                IsBusy = false;

                if (result.TransactionResult == TransactionResultTypes.Ok)
                {
                    RemoveHeaderSection("Invitation");
                    await MessageAsync("InvitationSuccess");
                }
                else
                {
                    await ErrorTextAsync(result.GetErrorMessage());
                }
            }
        }

        async Task Approve(ButtonRow arg)
        {
            var invitation = arg.Tag as FriendInvitation;
            if (await ConfirmAsync("ApprovalConfirm"))
            {
                IsBusy = true;
                var result = (await _node.AcceptFriendInvitation(invitation.FriendAccountId)).Result;
                IsBusy = false;

                if (result.TransactionResult == TransactionResultTypes.Ok)
                {
                    RemoveHeaderSection("Invitation");
                    await MessageAsync("ApprovalSuccess");
                    await PopAsync(2);
                }
                else
                {
                    await ErrorTextAsync(result.GetErrorMessage());
                }
            }
        }
    }
}
