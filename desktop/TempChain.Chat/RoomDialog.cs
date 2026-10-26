using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace TempChain.Chat;

public sealed class RoomDialog : Window
{
    private readonly TextBox name = new() { MaxLength = 64 };
    private readonly TextBox purpose = new() { MaxLength = 256 };
    private readonly CheckBox publicRoom = new() { Content = "공개 채팅방" };
    private readonly List<MemberChoice> choices;
    public string RoomName => name.Text.Trim();
    public string Purpose => purpose.Text.Trim();
    public bool IsPublic => publicRoom.IsChecked == true;
    public long[] ParticipantIds => choices.Where(member => member.Selected).Select(member => member.Id).ToArray();

    public RoomDialog(IEnumerable<ContactItem> contacts, bool inviteOnly, bool allowPublic, bool allowInvites = true)
    {
        Title = inviteOnly ? "직원 초대" : "새 채팅방 만들기";
        Width = 520; Height = inviteOnly ? 540 : 700; MinWidth = 440; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        choices = contacts.Where(contact => !contact.IsOwn).Select(contact => new MemberChoice(contact.User.Id, contact.Title, contact.User.UserName)).ToList();
        var grid = new Grid { Margin = new Thickness(24) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = Title, FontSize = 23, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,16) });
        if (!inviteOnly)
        {
            header.Children.Add(new Label { Content = "채팅방 이름", Target = name }); header.Children.Add(name);
            header.Children.Add(new Label { Content = "설명 (선택)", Target = purpose }); header.Children.Add(purpose);
            publicRoom.IsEnabled = allowPublic; header.Children.Add(publicRoom);
        }
        header.Children.Add(new TextBlock { Text = allowInvites ? "초대할 직원 선택" : "직원 초대 권한이 없어 본인만 참여하는 방을 만듭니다.", TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Brushes.SlateGray, Margin = new Thickness(0,8,0,12) });
        var search = new TextBox { ToolTip = "직원 이름 검색", Margin = new Thickness(0,0,0,10) };
        AutomationProperties.SetName(search, "초대할 직원 검색");
        header.Children.Add(search); grid.Children.Add(header);
        var view = new ListCollectionView(choices);
        var list = new ListBox { ItemsSource = view, BorderThickness = new Thickness(0), Background = Brushes.White, IsEnabled = allowInvites };
        var template = new DataTemplate(typeof(MemberChoice));
        var check = new FrameworkElementFactory(typeof(CheckBox));
        check.SetBinding(ContentControl.ContentProperty, new Binding(nameof(MemberChoice.Label)));
        check.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(MemberChoice.Selected)) { Mode = BindingMode.TwoWay });
        check.SetValue(FrameworkElement.MarginProperty, new Thickness(12,8,12,8));
        template.VisualTree = check; list.ItemTemplate = template;
        search.TextChanged += (_, _) => { var term = search.Text.Trim(); view.Filter = value => ((MemberChoice)value).Label.Contains(term, StringComparison.CurrentCultureIgnoreCase); };
        Grid.SetRow(list,1); grid.Children.Add(list);
        var footer = new StackPanel { Margin = new Thickness(0,14,0,0) };
        var error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,10) }; footer.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(new Button { Content = "취소", IsCancel = true });
        var submit = new Button { Content = inviteOnly ? "선택한 직원 초대" : "채팅방 만들기", IsDefault = true, Style = (Style)Application.Current.FindResource("PrimaryButton") };
        submit.Click += (_, _) => {
            if (!inviteOnly && string.IsNullOrWhiteSpace(RoomName)) { error.Text = "방 이름을 입력해 주세요."; name.Focus(); return; }
            if (inviteOnly && ParticipantIds.Length == 0) { error.Text = "초대할 직원을 선택해 주세요."; return; }
            if (ParticipantIds.Length > 200) { error.Text = "한 번에 최대 200명까지 선택할 수 있습니다."; return; }
            DialogResult = true;
        };
        buttons.Children.Add(submit); footer.Children.Add(buttons); Grid.SetRow(footer,2); grid.Children.Add(footer); Content = grid;
        Loaded += (_, _) => { if (inviteOnly) search.Focus(); else name.Focus(); };
    }

    public sealed class MemberChoice
    {
        public long Id { get; }
        public string Label { get; }
        public bool Selected { get; set; }
        public MemberChoice(long id, string name, string account) { Id = id; Label = $"{name}  ·  {account}"; }
    }
}

public sealed class CloseRoomDialog : Window
{
    public CloseRoomDialog(string roomName)
    {
        Title = "전체 채팅방 닫기"; Width = 500; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = "전체 참여자에게 영향을 줍니다", FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Firebrick });
        panel.Children.Add(new TextBlock { Text = "모든 참여자가 이 방을 사용할 수 없게 됩니다. 기록은 서버 보존 정책에 따릅니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,16,0,14), LineHeight = 23 });
        panel.Children.Add(new TextBlock { Text = "확인할 방 이름: " + roomName, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,10) });
        var input = new TextBox(); AutomationProperties.SetName(input, "삭제할 채팅방 이름을 그대로 입력"); panel.Children.Add(input);
        var consent = new CheckBox { Content = "다른 참여자에게도 영향을 주는 것을 이해했습니다." }; panel.Children.Add(consent);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,14,0,0) };
        buttons.Children.Add(new Button { Content = "취소", IsCancel = true });
        var close = new Button { Content = "전체 방 닫기", Foreground = Brushes.White, Background = Brushes.Firebrick, IsEnabled = false };
        void Update() => close.IsEnabled = input.Text == roomName && consent.IsChecked == true;
        input.TextChanged += (_, _) => Update(); consent.Checked += (_, _) => Update(); consent.Unchecked += (_, _) => Update();
        close.Click += (_, _) => DialogResult = true; buttons.Children.Add(close); panel.Children.Add(buttons); Content = panel;
    }
}
