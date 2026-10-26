using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TempChain.Chat;

/// <summary>App-owned popup: unlike Windows balloons its duration is controlled by this app.</summary>
public sealed class NotificationWindow : Window
{
    private readonly DispatcherTimer timer;
    private readonly bool animate;
    private bool dismissing;

    public NotificationWindow(string title, string detail, int seconds, bool animation, Action? activate = null)
    {
        Width = 360; Height = 150;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Title = "TempChain Chat 알림";
        animate = animation && SystemParameters.ClientAreaAnimation;
        var panel = new Grid { Margin = new Thickness(16) };
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var brand = new TextBlock { Text = "TEMPCHAIN  ·  새 알림", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(89, 123, 154)) };
        panel.Children.Add(brand);
        var dismiss = new Button { Content = "닫기", FontSize = 10, Padding = new Thickness(7, 3, 7, 3), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
        dismiss.Click += (_, e) => { e.Handled = true; Dismiss(); };
        panel.Children.Add(dismiss);
        var open = new Button { Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, Padding = new Thickness(0, 8, 0, 0), Margin = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch, Cursor = Cursors.Hand };
        var content = new StackPanel { Width = 300 };
        content.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 15, TextTrimming = TextTrimming.CharacterEllipsis });
        content.Children.Add(new TextBlock { Text = detail, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(99, 122, 145)), Margin = new Thickness(0, 8, 0, 0) });
        open.Content = content;
        open.Click += (_, _) => { Dismiss(); activate?.Invoke(); };
        Grid.SetRow(open, 1); panel.Children.Add(open);
        Content = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 221, 237)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Margin = new Thickness(5), Child = panel };
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(seconds, 3, 5)) };
        timer.Tick += (_, _) => Dismiss();
        Loaded += (_, _) => {
            var work = SystemParameters.WorkArea;
            Left = Math.Max(work.Left, work.Right - ActualWidth - 16);
            Top = Math.Max(work.Top, work.Bottom - ActualHeight - 16);
            if (animate) BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            timer.Start();
        };
        Closed += (_, _) => timer.Stop();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Dismiss(); } };
    }

    private void Dismiss()
    {
        if (dismissing) return;
        dismissing = true; timer.Stop();
        if (!animate) { Close(); return; }
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}
