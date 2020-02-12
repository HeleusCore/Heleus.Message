using System;
using System.Threading.Tasks;
using Heleus.ProfileService;
using Xamarin.Forms;

namespace Heleus.Apps.Shared
{
    public class ChatProfileButtonRow : ProfileButtonRow
    {
        readonly ExtLabel _countLabel = new ExtLabel();

        public void UpdateMessagesCount(long count)
        {
            if (_countLabel != null)
            {
                var text = count <= 0 ? null : $"+{count}";
                if (_countLabel.Text != text)
                    _countLabel.Text = text;
            }
        }

        public ChatProfileButtonRow(ServiceNode serviceNode, long accountId, ProfileInfo profileInfo, ProfileDataResult profileData, Func<ProfileButtonRow, Task> action, bool showCount) : base(accountId, profileInfo, profileData, action, AccentColorExtenstion.DefaultAccentColorWith)
        {
            if (showCount)
            {
                RowLayout.Children.Remove(FontIcon);

                _countLabel.InputTransparent = true;
                _countLabel.FontStyle = Theme.DetailFont;
                _countLabel.ColorStyle = Theme.TextColor;

                _countLabel.Margin = new Thickness(0, 0, 10, 0);

                AbsoluteLayout.SetLayoutFlags(_countLabel, AbsoluteLayoutFlags.PositionProportional);
                RowLayout.Children.Add(_countLabel, new Point(1, 0.5));
            }

            RowLayout.SetAccentColor(serviceNode.AccentColor);
        }
    }
}
