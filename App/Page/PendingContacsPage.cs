using System;
using System.Threading.Tasks;
using Heleus.Apps.Message;
using Heleus.Chain.Data;
using Heleus.Transactions;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Shared
{
    public class PendingContactsPage : StackPage
    {
        readonly MessageNode _node;

        public PendingContactsPage(MessageNode node, FriendInfo friendInfo) : base("PendingContactsPage")
        {
            Subscribe<AcceptInvitationEvent>(AcceptInvitation);
            _node = node;

            AddTitleRow("Title");

            var invitations = friendInfo?.Invitations;
            var count = 0;
            var nodeInvitations = true;

            if (invitations != null)
            {
                foreach (var invitation in invitations)
                {
                    if (invitation.HasFriendAccountApproval)
                    {
                        count++;
                        nodeInvitations = false;
                    }
                }
            }

            if (count > 0)
            {
                AddHeaderRow("RequiredApproval");
                foreach (var invitation in invitations)
                {
                    if (invitation.HasFriendAccountApproval)
                    {
                        var row = AddRow(new ProfileButtonRow(invitation.FriendAccountId, Confirm));
                        row.Tag = invitation;
                    }
                }
                AddFooterRow();
            }

            count = 0;
            if (invitations != null)
            {
                foreach (var invitation in invitations)
                {
                    if (invitation.HasAccountApproval)
                    {
                        count++;
                        nodeInvitations = false;
                    }
                }
            }

            if (count > 0)
            {
                AddHeaderRow("AwaitingApproval");
                foreach (var invitation in invitations)
                {
                    if (invitation.HasAccountApproval)
                    {
                        var row = AddRow(new ProfileButtonRow(invitation.FriendAccountId, Awaiting));
                        row.Tag = invitation;
                    }
                }
                AddFooterRow();
            }

            if(nodeInvitations)
            {
                AddHeaderRow("NoInvitations");
                AddInfoRow("NoInvitationsInfo");
                AddFooterRow();
            }
        }

        Task AcceptInvitation(AcceptInvitationEvent arg)
        {
            var result = arg.Result;
            if (result.TransactionResult == TransactionResultTypes.Ok)
            {
                var rows = GetHeaderSectionRows("RequiredApproval");
                foreach (var row in rows)
                {
                    if (row.Tag is FriendInvitation fi)
                    {
                        if (fi.FriendAccountId == arg.AccountId)
                        {
                            RemoveView(row);
                            break;
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        async Task Awaiting(ProfileButtonRow arg)
        {
            await Navigation.PushAsync(new ContactPage(_node, arg.AccountId));
        }

        async Task Confirm(ProfileButtonRow arg)
        {
            await Navigation.PushAsync(new ContactPage(_node, arg.AccountId));
        }
    }
}
