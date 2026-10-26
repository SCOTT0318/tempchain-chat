using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TempChain.Chat.Models;
using TempChain.Chat.Services;
using Forms = System.Windows.Forms;

namespace TempChain.Chat;

public partial class MainWindow : Window
{
    private readonly SettingsStore store;
    private AppSettings settings;
    private SynologyChatClient? client;
    private CancellationTokenSource? sessionCancellation;
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private Forms.NotifyIcon? tray;
    private bool exitRequested;
    private bool firstHide = true;
    private bool signingIn;
    private bool sending;
    private bool openingConversation;
    private bool hidingConversation;
    private bool showingContacts;
    private bool managingRoom;
    private readonly NotificationTracker notificationTracker = new();
    private NotificationWindow? notification;
    private MenuItem createRoomMenu = null!, inviteRoomMenu = null!, leaveRoomMenu = null!, closeRoomMenu = null!;
    private long? selectedChannelId;
    private long? messagesChannelId;
    private int sessionGeneration;
    private bool directoryUpdating;
    private readonly HashSet<long> seenMessageIds = new();
    private readonly Dictionary<long, long> displayedMessageAt = new();
    private readonly Dictionary<long, long> readMessageAt = new();
    public ObservableCollection<ConversationItem> Conversations { get; } = new();
    public ObservableCollection<ContactItem> Contacts { get; } = new();
    public ObservableCollection<MessageItem> Messages { get; } = new();

    public MainWindow(SettingsStore store)
    {
        InitializeComponent();
        this.store = store;
        settings = store.Load();
        DataContext = this;
        CollectionViewSource.GetDefaultView(Conversations).Filter = item => MatchesSearch(((ConversationItem)item).Title);
        CollectionViewSource.GetDefaultView(Contacts).Filter = item => MatchesSearch(((ContactItem)item).Title);
        ConfigureConversationMenu();
        ConfigureRoomMenu();
        try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/app.ico")); }
        catch (IOException) { }
        CreateTray();
    }

    private void ConfigureConversationMenu()
    {
        var hide = CreateMenuItem("대화 목록에서 숨기기", "\uE8D4");
        hide.Click += HideConversation_Click;
        var menu = new ContextMenu();
        menu.Items.Add(hide);
        menu.Opened += (_, _) => hide.IsEnabled = !hidingConversation &&
            ConversationsList.SelectedItem is ConversationItem { Data.IsSelf: false } && client is not null;
        ConversationsList.ContextMenu = menu;
        ConversationsList.PreviewMouseRightButtonDown += (_, args) => {
            var element = args.OriginalSource as DependencyObject;
            var item = element is null ? null : ItemsControl.ContainerFromElement(ConversationsList, element) as ListBoxItem;
            if (item is not null) item.IsSelected = true;
            else args.Handled = true;
        };
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("TChat 열기", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("새로 고침", null, (_, _) => Dispatcher.Invoke(() => { _ = RefreshChatAsync(true); }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(Exit));
        System.Drawing.Icon icon;
        try { icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? SystemIcons.Application; }
        catch { icon = SystemIcons.Application; }
        tray = new Forms.NotifyIcon { Text = "TChat", Icon = icon, Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UsernameBox.Focus();
        if (store.LoadWarning is not null) LoginErrorText.Text = store.LoadWarning;
        AppUpdateManager.Instance.Changed += RefreshUpdateBanner;
        Closed += (_, _) => AppUpdateManager.Instance.Changed -= RefreshUpdateBanner;
        AppUpdateManager.Instance.Configure(settings.AutoUpdate);
        RefreshUpdateBanner();
    }

    private void RefreshUpdateBanner()
    {
        var updates = AppUpdateManager.Instance;
        UpdateBanner.Visibility = updates.IsReady ? Visibility.Visible : Visibility.Collapsed;
        UpdateBannerText.Text = $"v{updates.LatestVersionText} 업데이트 준비됨";
        InstallUpdateButton.IsEnabled = updates.CanInstall;
    }

    private void InstallUpdate_Click(object sender, RoutedEventArgs e) => AppUpdateManager.Instance.InstallAndRestart();
    public void ShutdownForUpdate() { exitRequested = true; Application.Current.Shutdown(); }
    public bool CanRestartForUpdate => !signingIn && !sending && !openingConversation && !hidingConversation && !managingRoom && string.IsNullOrWhiteSpace(MessageInput.Text);

    private async void Login_Click(object sender, RoutedEventArgs e) => await SignInAsync();

    private async Task SignInAsync()
    {
        if (signingIn || client is not null) return;
        var username = UsernameBox.Text.Trim();
        if (username.Length == 0 || LoginPasswordBox.Password.Length == 0)
        { LoginErrorText.Text = "사용자 이름과 비밀번호를 입력해 주세요."; return; }
        if (!NavigationPolicy.TryGetServerUri(ServerConfiguration.DefaultServerUrl, out var server))
        { LoginErrorText.Text = "메신저 연결 설정을 확인하지 못했습니다. 관리자에게 문의해 주세요."; return; }
        signingIn = true;
        LoginButton.IsEnabled = false;
        LoginButton.Content = "연결하는 중…";
        LoginErrorText.Text = "";
        sessionCancellation = new CancellationTokenSource();
        var cancellation = sessionCancellation;
        var generation = ++sessionGeneration;
        var candidate = new SynologyChatClient(server!);
        try
        {
            await candidate.LoginAsync(username, LoginPasswordBox.Password, cancellation.Token);
            if (generation != sessionGeneration || cancellation.IsCancellationRequested) return;
            var user = candidate.CurrentUser ?? throw new InvalidOperationException();
            client = candidate;
            try { store.Save(settings); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            MyNameText.Text = DisplayName(user);
            MyInitialText.Text = Initial(DisplayName(user));
            LoginView.Visibility = Visibility.Collapsed;
            ChatView.Visibility = Visibility.Visible;
            StatusText.Text = "연락처와 대화를 불러오는 중";
            await RefreshChatAsync(true, waitForGate: true);
            if (generation == sessionGeneration && client is not null) _ = PollAsync(generation, cancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ReferenceEquals(client, candidate)) client = null;
            LoginErrorText.Text = FriendlyError(ex);
        }
        finally
        {
            if (!ReferenceEquals(client, candidate)) candidate.Dispose();
            LoginPasswordBox.Clear();
            signingIn = false;
            LoginButton.IsEnabled = true;
            LoginButton.Content = "로그인";
        }
    }

    private async Task PollAsync(int generation, CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
            while (await timer.WaitForNextTickAsync(token))
            {
                if (generation != sessionGeneration) return;
                await RefreshChatAsync(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task RefreshChatAsync(bool refreshDirectory, bool waitForGate = false)
    {
        var active = client;
        var cancellation = sessionCancellation;
        if (active is null || cancellation is null || cancellation.IsCancellationRequested) return;
        var token = cancellation.Token;
        var generation = sessionGeneration;
        bool entered = false;
        try
        {
            if (waitForGate) { await refreshGate.WaitAsync(token); entered = true; }
            else entered = await refreshGate.WaitAsync(0, token);
            if (!entered || active != client || generation != sessionGeneration) return;
            var conversations = await active.GetConversationsAsync(token);
            if (active != client || generation != sessionGeneration) return;
            if (refreshDirectory || Contacts.Count == 0)
            {
                var users = await active.GetUsersAsync(token);
                if (active != client || generation != sessionGeneration) return;
                Contacts.Clear();
                foreach (var user in users.Where(user => user.IsActive).OrderBy(DisplayName))
                    Contacts.Add(new ContactItem(user, user.Id == active.CurrentUser?.Id));
            }
            var notifications = notificationTracker.Observe(active.NotificationConversations);
            ApplyConversations(conversations);
            var incoming = new List<(ChatConversation Room, string Sender)>();
            {
                foreach (var room in notifications.Where(room => !room.IsEncrypted &&
                    !(room.Id == selectedChannelId && IsVisible && IsActive && WindowState != WindowState.Minimized)))
                {
                    try
                    {
                        // Confirm the newest author; existing unread messages must not make our own send trigger a popup.
                        var posts = await active.GetMessagesAsync(room.Id, token);
                        if (posts.LastOrDefault() is { IsOwn: false } newest) incoming.Add((room, newest.UserName));
                    }
                    catch (SynologyApiException) { /* An inaccessible/deleted room must not stop message polling. */ }
                    if (active != client || generation != sessionGeneration) return;
                }
            }
            ShowIncomingNotifications(incoming, generation);
            if (refreshDirectory)
            {
                try { await active.RefreshRoomPermissionsAsync(token); }
                catch (SynologyApiException) { /* Keep messaging available; room actions fail closed. */ }
                UpdateRoomActions();
            }
            var channelAtStart = selectedChannelId;
            if (channelAtStart is long channelId)
            {
                var result = await active.GetMessagesAsync(channelId, token);
                if (active != client || generation != sessionGeneration || selectedChannelId != channelAtStart) return;
                ApplyMessages(channelId, result);
                // Let native item layout/render run before treating visible text
                // as read. Hidden/minimized/background windows never mark read.
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                if (active != client || generation != sessionGeneration || selectedChannelId != channelId) return;
                if (IsVisible && IsActive && WindowState != WindowState.Minimized && Messages.Count > 0)
                {
                    var lastShown = Messages.Max(message => message.Message.SentAt.ToUnixTimeMilliseconds());
                    displayedMessageAt[channelId] = lastShown;
                    if (!readMessageAt.TryGetValue(channelId, out var previousRead) || previousRead < lastShown)
                    {
                        await active.MarkConversationReadAsync(channelId, lastShown, token);
                        if (active == client && generation == sessionGeneration) readMessageAt[channelId] = lastShown;
                    }
                }
            }
            StatusText.Text = $"연락처 {Contacts.Count}명";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        { if (generation == sessionGeneration && active == client) StatusText.Text = FriendlyError(ex); }
        finally { if (entered) refreshGate.Release(); }
    }

    private void ApplyConversations(IReadOnlyList<ChatConversation> result)
    {
        directoryUpdating = true;
        try
        {
            var previous = selectedChannelId;
            var old = Conversations.ToDictionary(item => item.Id);
            var desired = result.Select(conversation => {
                var title = ConversationTitle(conversation);
                if (old.TryGetValue(conversation.Id, out var item)) { item.Update(conversation, title); return item; }
                return new ConversationItem(conversation, title);
            }).ToList();
            for (var index = 0; index < desired.Count; index++)
            {
                if (index < Conversations.Count && Conversations[index].Id == desired[index].Id) continue;
                var existing = Conversations.IndexOf(desired[index]);
                if (existing >= 0) Conversations.Move(existing, index);
                else Conversations.Insert(index, desired[index]);
            }
            while (Conversations.Count > desired.Count) Conversations.RemoveAt(Conversations.Count - 1);
            if (previous is not null)
            {
                ConversationsList.SelectedItem = Conversations.FirstOrDefault(item => item.Id == previous);
                if (ConversationsList.SelectedItem is null) ClearSelection();
                else SetConversationHeader((ConversationItem)ConversationsList.SelectedItem);
            }
            CollectionViewSource.GetDefaultView(Conversations).Refresh();
            UpdateDirectoryEmpty();
        }
        finally { directoryUpdating = false; }
    }

    private string ConversationTitle(ChatConversation conversation)
    {
        if (!string.IsNullOrWhiteSpace(conversation.Name)) return conversation.Name;
        var members = conversation.MemberIds.Where(id => id != client?.CurrentUser?.Id)
            .Select(id => Contacts.FirstOrDefault(contact => contact.User.Id == id)?.Title).Where(name => name is not null);
        var title = string.Join(", ", members);
        return title.Length > 0 ? title : conversation.IsSelf ? "나에게 보내기" : $"대화 {conversation.Id}";
    }

    private void ApplyMessages(long channelId, IReadOnlyList<ChatMessage> messages)
    {
        var firstLoad = messagesChannelId != channelId;
        var ordered = messages.Where(message => !string.IsNullOrWhiteSpace(message.Text) || message.HasAttachments)
            .OrderBy(message => message.SentAt).ThenBy(message => message.Id).ToList();
        var nearBottom = MessagesScroll.ScrollableHeight - MessagesScroll.VerticalOffset < 50;
        if (firstLoad) { seenMessageIds.Clear(); Messages.Clear(); }
        foreach (var message in ordered) seenMessageIds.Add(message.Id);
        if (Messages.Count != ordered.Count || !Messages.Select(item => item.Message).SequenceEqual(ordered))
        {
            Messages.Clear();
            foreach (var message in ordered) Messages.Add(new MessageItem(message));
            if (firstLoad || nearBottom) Dispatcher.BeginInvoke(MessagesScroll.ScrollToEnd);
        }
        messagesChannelId = channelId;
        MessageEmptyPanel.Visibility = Messages.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHeadingText.Text = "아직 메시지가 없습니다";
        EmptyDetailText.Text = "";
    }

    private async void ConversationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (directoryUpdating) return;
        if (ConversationsList.SelectedItem is not ConversationItem item) { ClearSelection(); return; }
        if (selectedChannelId == item.Id) return;
        selectedChannelId = item.Id;
        messagesChannelId = null;
        Messages.Clear(); seenMessageIds.Clear(); MessageInput.Clear();
        SetConversationHeader(item);
        MessageEmptyPanel.Visibility = Visibility.Visible;
        EmptyHeadingText.Text = "대화를 불러오는 중";
        EmptyDetailText.Text = "";
        await RefreshChatAsync(false, waitForGate: true);
    }

    private void SetConversationHeader(ConversationItem item)
    {
        ChatTitleText.Text = item.Title;
        ChatDetailText.Text = item.Data.IsEncrypted ? "암호화 대화" : item.Data.MemberIds.Count > 0 ? $"참여자 {item.Data.MemberIds.Count}명" : "팀 대화";
        EncryptedNotice.Visibility = item.Data.IsEncrypted ? Visibility.Visible : Visibility.Collapsed;
        MessageInput.IsEnabled = !item.Data.IsEncrypted;
        SendButton.IsEnabled = !item.Data.IsEncrypted && !sending;
        HideConversationButton.IsEnabled = !item.Data.IsSelf && !hidingConversation;
        StartDirectButton.Visibility = showingContacts ? Visibility.Visible : Visibility.Collapsed;
        UpdateRoomActions();
    }

    private void ClearSelection()
    {
        selectedChannelId = null; messagesChannelId = null;
        Messages.Clear(); seenMessageIds.Clear(); MessageInput.Clear();
        MessageInput.IsEnabled = false; SendButton.IsEnabled = false;
        HideConversationButton.IsEnabled = false;
        UpdateRoomActions();
        EncryptedNotice.Visibility = Visibility.Collapsed;
        ChatTitleText.Text = "우리 팀과 대화를 시작해 보세요";
        ChatDetailText.Text = "";
        MessageEmptyPanel.Visibility = Visibility.Visible;
        EmptyHeadingText.Text = "대화를 선택하세요";
        EmptyDetailText.Text = "연락처에서 대화를 시작하세요.";
    }

    private async void HideConversation_Click(object sender, RoutedEventArgs e)
    {
        if (hidingConversation || client is null || sessionCancellation is null ||
            ConversationsList.SelectedItem is not ConversationItem item || item.Data.IsSelf) return;
        var description = "이 대화를 내 대화 목록에서 숨기시겠습니까?\n\n메시지는 삭제되지 않으며 다른 참여자의 대화에는 영향을 주지 않습니다.";
        if (item.Data.Kind == "direct") description += "\n연락처에서 다시 대화를 열 수 있습니다.";
        if (MessageBox.Show(this, description, "대화 목록에서 숨기기", MessageBoxButton.YesNo,
            MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var active = client;
        var generation = sessionGeneration;
        var token = sessionCancellation.Token;
        var entered = false;
        hidingConversation = true;
        HideConversationButton.IsEnabled = false;
        try
        {
            await refreshGate.WaitAsync(token);
            entered = true;
            if (active != client || generation != sessionGeneration) return;
            if (selectedChannelId == item.Id && displayedMessageAt.TryGetValue(item.Id, out var lastDisplayed))
                await active.MarkConversationReadAsync(item.Id, lastDisplayed, token);
            await active.HideConversationAsync(item.Id, token);
            refreshGate.Release();
            entered = false;
            if (active != client || generation != sessionGeneration) return;
            if (selectedChannelId == item.Id)
            {
                ConversationsList.SelectedItem = null;
                ClearSelection();
            }
            await RefreshChatAsync(false, waitForGate: true);
            StatusText.Text = "내 대화 목록에서 숨겼습니다. 메시지는 그대로 유지됩니다.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        { if (active == client) StatusText.Text = "대화를 숨기지 못했습니다. " + FriendlyError(ex); }
        finally
        {
            if (entered) refreshGate.Release();
            hidingConversation = false;
            HideConversationButton.IsEnabled = ConversationsList.SelectedItem is ConversationItem { Data.IsSelf: false } && client is not null;
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendAsync();
    private async Task SendAsync()
    {
        var active = client;
        var token = sessionCancellation?.Token ?? CancellationToken.None;
        var generation = sessionGeneration;
        var channelId = selectedChannelId;
        var text = MessageInput.Text.Trim();
        if (active is null || channelId is null || sending || string.IsNullOrEmpty(text)) return;
        if (Conversations.FirstOrDefault(item => item.Id == channelId)?.Data.IsEncrypted != false) return;
        if (text.Length > 4000) { StatusText.Text = "메시지는 4,000자 이내로 입력해 주세요."; return; }
        sending = true; SendButton.IsEnabled = false;
        var entered = false;
        try
        {
            await refreshGate.WaitAsync(token);
            entered = true;
            if (active != client || generation != sessionGeneration) return;
            await active.SendMessageAsync(channelId.Value, text, token);
            refreshGate.Release();
            entered = false;
            if (active != client || generation != sessionGeneration) return;
            if (selectedChannelId == channelId && MessageInput.Text.Trim() == text) MessageInput.Clear();
            StatusText.Text = "메시지를 보냈습니다";
            await RefreshChatAsync(false, waitForGate: true);
            if (selectedChannelId == channelId) _ = Dispatcher.BeginInvoke(MessagesScroll.ScrollToEnd);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (active == client) StatusText.Text = FriendlyError(ex); }
        finally
        {
            if (entered) refreshGate.Release();
            sending = false;
            if (client is not null && selectedChannelId is not null)
                SendButton.IsEnabled = Conversations.FirstOrDefault(item => item.Id == selectedChannelId)?.Data.IsEncrypted == false;
        }
    }

    private async void StartDirect_Click(object sender, RoutedEventArgs e) => await OpenSelectedContactAsync();
    private async void ContactsList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => await OpenSelectedContactAsync();
    private async void ContactsList_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { e.Handled = true; await OpenSelectedContactAsync(); } }

    private async Task OpenSelectedContactAsync()
    {
        if (openingConversation || client is null || sessionCancellation is null) return;
        if (ContactsList.SelectedItem is not ContactItem contact)
        { StatusText.Text = "대화를 시작할 연락처를 먼저 선택해 주세요."; return; }
        var active = client;
        var generation = sessionGeneration;
        var token = sessionCancellation.Token;
        var entered = false;
        openingConversation = true; StartDirectButton.IsEnabled = false;
        try
        {
            StatusText.Text = "대화를 준비하는 중";
            await refreshGate.WaitAsync(token);
            entered = true;
            if (active != client || generation != sessionGeneration) return;
            var result = await active.OpenDirectConversationAsync(contact.User.Id, token);
            refreshGate.Release();
            entered = false;
            if (active != client || generation != sessionGeneration) return;
            await RefreshChatAsync(false, waitForGate: true);
            Conversations_Click(this, new RoutedEventArgs());
            var item = Conversations.FirstOrDefault(conversation => conversation.Id == result.Id);
            if (item is null) StatusText.Text = "대화를 만들었지만 목록에서 찾지 못했습니다. 새로 고침해 주세요.";
            else ConversationsList.SelectedItem = item;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (active == client) StatusText.Text = FriendlyError(ex); }
        finally
        {
            if (entered) refreshGate.Release();
            openingConversation = false; StartDirectButton.IsEnabled = true;
        }
    }

    private void Conversations_Click(object sender, RoutedEventArgs e)
    {
        showingContacts = false;
        ConversationsList.Visibility = Visibility.Visible; ContactsList.Visibility = Visibility.Collapsed;
        DirectoryTitle.Text = "대화"; StartDirectButton.Visibility = Visibility.Collapsed;
        DirectorySearch.Clear(); UpdateDirectoryEmpty();
    }

    private void Contacts_Click(object sender, RoutedEventArgs e)
    {
        showingContacts = true;
        ConversationsList.Visibility = Visibility.Collapsed; ContactsList.Visibility = Visibility.Visible;
        DirectoryTitle.Text = "연락처"; StartDirectButton.Visibility = Visibility.Visible;
        DirectorySearch.Clear(); UpdateDirectoryEmpty();
    }

    private bool MatchesSearch(string name) => string.IsNullOrWhiteSpace(DirectorySearch?.Text) ||
        name.Contains(DirectorySearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private void DirectorySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized || DataContext is null) return;
        CollectionViewSource.GetDefaultView(Conversations).Refresh();
        CollectionViewSource.GetDefaultView(Contacts).Refresh();
        UpdateDirectoryEmpty();
    }

    private void UpdateDirectoryEmpty()
    {
        var count = showingContacts ? CollectionViewSource.GetDefaultView(Contacts).Cast<object>().Count() :
            CollectionViewSource.GetDefaultView(Conversations).Cast<object>().Count();
        DirectoryEmptyText.Text = showingContacts ? "표시할 연락처가 없습니다." : "대화가 없습니다. 연락처에서 시작하세요.";
        DirectoryEmptyText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static MenuItem CreateMenuItem(string label, string glyph) => new()
    {
        Header = label,
        Icon = new TextBlock { Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), FontSize = 14 }
    };

    private void ConfigureRoomMenu()
    {
        var menu = new ContextMenu();
        createRoomMenu = CreateMenuItem("새 채팅방 만들기", "\uE710"); createRoomMenu.Click += CreateRoom_Click;
        inviteRoomMenu = CreateMenuItem("직원 초대", "\uE8FA"); inviteRoomMenu.Click += InviteRoom_Click;
        leaveRoomMenu = CreateMenuItem("채팅방 나가기", "\uE8BB"); leaveRoomMenu.Click += LeaveRoom_Click;
        closeRoomMenu = CreateMenuItem("전체 채팅방 닫기…", "\uE74D");
        closeRoomMenu.Style = (Style)FindResource("DangerMenuItem"); closeRoomMenu.Click += CloseRoom_Click;
        menu.Items.Add(createRoomMenu); menu.Items.Add(inviteRoomMenu); menu.Items.Add(leaveRoomMenu);
        menu.Items.Add(new Separator()); menu.Items.Add(closeRoomMenu);
        menu.Opened += (_, _) => UpdateRoomActions(); RoomActionsButton.ContextMenu = menu;
    }

    private void RoomActions_Click(object sender, RoutedEventArgs e)
    {
        UpdateRoomActions();
        RoomActionsButton.ContextMenu.PlacementTarget = RoomActionsButton;
        RoomActionsButton.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        RoomActionsButton.ContextMenu.IsOpen = true;
    }

    private void UpdateRoomActions()
    {
        if (createRoomMenu is null) return;
        var room = (ConversationsList.SelectedItem as ConversationItem)?.Data;
        var named = room is { IsNamedRoom: true, IsEncrypted: false };
        createRoomMenu.IsEnabled = !managingRoom && client?.CanCreateRooms == true;
        inviteRoomMenu.IsEnabled = !managingRoom && named && client?.CanInviteMembers == true;
        leaveRoomMenu.IsEnabled = !managingRoom && named && client?.CanLeaveRooms == true;
        closeRoomMenu.IsEnabled = !managingRoom && named && client?.CanCloseRooms == true;
        foreach (var action in new[] { createRoomMenu, inviteRoomMenu, leaveRoomMenu, closeRoomMenu })
            action.ToolTip = action.IsEnabled ? null : "새로 고침 후 NAS 권한을 확인하세요. 기본방·일대일·암호화방의 변경은 제한됩니다.";
    }

    private async void CreateRoom_Click(object sender, RoutedEventArgs e)
    {
        var active = client;
        if (managingRoom || active?.CanCreateRooms != true) return;
        var dialog = new RoomDialog(Contacts, false, active.CanCreatePublicRooms, active.CanInviteMembers) { Owner = this };
        if (dialog.ShowDialog() != true || active != client) return;
        await RunRoomOperationAsync(active, async (api, ct) => (await api.CreateRoomAsync(dialog.RoomName, dialog.Purpose, dialog.IsPublic, dialog.ParticipantIds, ct)).Id, "채팅방을 만들었습니다.");
    }

    private async void InviteRoom_Click(object sender, RoutedEventArgs e)
    {
        var active = client;
        if (managingRoom || active is null || ConversationsList.SelectedItem is not ConversationItem item) return;
        var dialog = new RoomDialog(Contacts.Where(contact => !item.Data.MemberIds.Contains(contact.User.Id)), true, false) { Owner = this };
        if (dialog.ShowDialog() != true || active != client) return;
        await RunRoomOperationAsync(active, async (api, ct) => { await api.InviteMembersAsync(item.Id, dialog.ParticipantIds, ct); return item.Id; }, "선택한 직원을 초대했습니다.");
    }

    private async void LeaveRoom_Click(object sender, RoutedEventArgs e)
    {
        var active = client;
        if (managingRoom || active is null || ConversationsList.SelectedItem is not ConversationItem item) return;
        if (MessageBox.Show(this, $"‘{item.Title}’ 채팅방에서 나갈까요?\n다른 참여자는 남아 있습니다. 비공개방에 다시 참여하려면 초대가 필요할 수 있습니다.", "채팅방 나가기",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await RunRoomOperationAsync(active, async (api, ct) => { await api.LeaveRoomAsync(item.Id, ct); return null; }, "채팅방에서 나왔습니다.");
    }

    private async void CloseRoom_Click(object sender, RoutedEventArgs e)
    {
        var active = client;
        if (managingRoom || active is null || ConversationsList.SelectedItem is not ConversationItem item) return;
        if (new CloseRoomDialog(item.Data.Name) { Owner = this }.ShowDialog() != true || active != client) return;
        await RunRoomOperationAsync(active, async (api, ct) => { await api.CloseRoomAsync(item.Id, item.Data.Name, ct); return null; }, "전체 채팅방을 닫았습니다. 기록의 보존·삭제는 NAS 정책을 따릅니다.");
    }

    private async Task RunRoomOperationAsync(SynologyChatClient active,
        Func<SynologyChatClient, CancellationToken, Task<long?>> operation, string success)
    {
        if (managingRoom || active != client || sessionCancellation is null) return;
        var generation = sessionGeneration; var ct = sessionCancellation.Token; var entered = false;
        managingRoom = true; UpdateRoomActions();
        try
        {
            await refreshGate.WaitAsync(ct); entered = true;
            if (active != client || generation != sessionGeneration) return;
            StatusText.Text = "채팅방 작업을 처리하는 중…";
            var select = await operation(active, ct);
            refreshGate.Release(); entered = false;
            if (active != client || generation != sessionGeneration) return;
            await RefreshChatAsync(true, waitForGate: true);
            if (active != client || generation != sessionGeneration) return;
            if (select is long id) { Conversations_Click(this, new RoutedEventArgs()); ConversationsList.SelectedItem = Conversations.FirstOrDefault(item => item.Id == id); }
            StatusText.Text = success;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (active == client && generation == sessionGeneration)
            {
                var message = FriendlyError(ex);
                // A create/join/invite sequence can partially succeed. Never replay the mutation.
                if (entered) { refreshGate.Release(); entered = false; }
                await RefreshChatAsync(true, waitForGate: true);
                if (active == client && generation == sessionGeneration)
                    StatusText.Text = message + " 결과가 불확실한 경우 공식 Chat에서 확인한 뒤 다시 시도하세요.";
            }
        }
        finally { if (entered) refreshGate.Release(); managingRoom = false; UpdateRoomActions(); }
    }

    private void ShowIncomingNotifications(IReadOnlyList<(ChatConversation Room, string Sender)> changed, int generation)
    {
        var rooms = changed.Where(item => !(item.Room.Id == selectedChannelId && IsVisible && IsActive && WindowState != WindowState.Minimized)).ToArray();
        if (rooms.Length == 0) return;
        var latest = rooms[^1];
        notification?.Close();
        var content = NotificationContent.Create(latest.Sender, latest.Room.Name, settings.ShowNotificationSender);
        var popup = new NotificationWindow(content.Title, content.Detail, settings.NotificationDurationSeconds, true,
            () => {
                if (client is null || generation != sessionGeneration) return;
                RestoreWindow(); Conversations_Click(this, new RoutedEventArgs());
                ConversationsList.SelectedItem = Conversations.FirstOrDefault(room => room.Id == latest.Room.Id);
            });
        notification = popup;
        popup.Closed += (_, _) => { if (ReferenceEquals(notification, popup)) notification = null; };
        popup.Show();

    }

    private async void Logout_Click(object sender, RoutedEventArgs e) => await SignOutAsync();
    private async Task SignOutAsync()
    {
        var old = client;
        client = null; ++sessionGeneration;
        notification?.Close(); notification = null; notificationTracker.Reset();
        sessionCancellation?.Cancel();
        if (old is not null)
        {
            var entered = false;
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await refreshGate.WaitAsync(timeout.Token);
                entered = true;
                await old.LogoutAsync(timeout.Token);
            }
            catch { }
            finally { if (entered) refreshGate.Release(); }
            old.Dispose();
        }
        sessionCancellation?.Dispose(); sessionCancellation = null;
        Conversations.Clear(); Contacts.Clear(); ClearSelection();
        displayedMessageAt.Clear(); readMessageAt.Clear();
        ChatView.Visibility = Visibility.Collapsed; LoginView.Visibility = Visibility.Visible;
        LoginErrorText.Text = ""; LoginPasswordBox.Clear();
        UsernameBox.Focus();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(store, settings) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        settings = dialog.Result;
        AppUpdateManager.Instance.Configure(settings.AutoUpdate);
        notification?.Close(); notification = null;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshChatAsync(true, waitForGate: true);
    private async void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        { e.Handled = true; await SendAsync(); }
    }
    private async void LoginField_KeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { e.Handled = true; await SignInAsync(); } }
    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.Key == Key.R) { e.Handled = true; await RefreshChatAsync(true, waitForGate: true); }
        if (e.Key == Key.OemComma) { e.Handled = true; Settings_Click(this, new RoutedEventArgs()); }
    }

    private static string FriendlyError(Exception exception) => exception switch {
        SynologyApiException api => api.Message,
        RoomOperationException partial => partial.Message,
        HttpRequestException => "메신저에 연결하지 못했습니다. 인터넷 연결을 확인한 뒤 다시 시도해 주세요. 계속되면 관리자에게 문의해 주세요.",
        TaskCanceledException => "서버 응답이 지연되고 있습니다. 잠시 후 다시 시도해 주세요.",
        _ => "요청을 처리하지 못했습니다. 연결 상태를 확인하고 다시 시도해 주세요."
    };
    private static string DisplayName(ChatUser user) => string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
    private static string Initial(string value) => string.IsNullOrEmpty(value) ? "T" : value[..1].ToUpperInvariant();

    public void RestoreWindow()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate(); Topmost = true; Topmost = false; Focus();
    }
    public void HideToTray(bool showBalloon = true)
    {
        Hide();
        if (showBalloon && firstHide && tray is not null)
        {
            tray.ShowBalloonTip(4000, "TChat", "알림 영역에서 계속 실행 중입니다.", Forms.ToolTipIcon.Info);
            firstHide = false;
        }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!exitRequested && settings.MinimizeToTray) { e.Cancel = true; HideToTray(); return; }
        notification?.Close();
        sessionCancellation?.Cancel(); client?.Dispose(); tray?.Dispose();
        Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
    }
    private void Exit() { exitRequested = true; Close(); }
}

public sealed class ConversationItem : INotifyPropertyChanged
{
    public ChatConversation Data { get; private set; }
    public long Id => Data.Id;
    public string Title { get; private set; }
    public string Initial => string.IsNullOrEmpty(Title) ? "#" : Title[..1].ToUpperInvariant();
    public string Subtitle => Data.IsEncrypted ? "암호화 대화" : Data.IsSelf ? "내 메모" : Data.Kind.Contains("channel", StringComparison.OrdinalIgnoreCase) ? "채널" : "메시지";
    public event PropertyChangedEventHandler? PropertyChanged;
    public ConversationItem(ChatConversation data, string title) { Data = data; Title = title; }
    public void Update(ChatConversation data, string title)
    {
        if (Data == data && Title == title) return;
        Data = data; Title = title; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}
public sealed class ContactItem
{
    public ChatUser User { get; }
    public bool IsOwn { get; }
    public string Title => (string.IsNullOrWhiteSpace(User.DisplayName) ? User.UserName : User.DisplayName) + (IsOwn ? " (나)" : "");
    public string Subtitle => IsOwn ? "내 계정 · " + User.UserName : User.UserName;
    public string Initial => string.IsNullOrEmpty(Title) ? "T" : Title[..1].ToUpperInvariant();
    public ContactItem(ChatUser user, bool isOwn = false) { User = user; IsOwn = isOwn; }
}
public sealed class MessageItem
{
    public ChatMessage Message { get; }
    public string Author => Message.UserName;
    public string Text => Message.Text;
    public bool IsOwn => Message.IsOwn;
    public string Time => Message.SentAt.ToLocalTime().ToString("tt h:mm");
    public string FullTime => Message.SentAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public Visibility AttachmentVisibility => Message.HasAttachments ? Visibility.Visible : Visibility.Collapsed;
    public MessageItem(ChatMessage message) => Message = message;
}
