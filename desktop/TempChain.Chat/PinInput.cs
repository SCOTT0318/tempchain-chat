using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;

namespace TempChain.Chat;

public sealed class PinInput : StackPanel
{
    private int length = 4;
    private bool assigning;
    public event EventHandler? PasswordChanged;
    public int Length
    {
        get => length;
        set { if (value is not (4 or 6)) throw new ArgumentOutOfRangeException(nameof(value)); length = value; Build(); }
    }
    public string Password
    {
        get => string.Concat(Children.Cast<PasswordBox>().Select(box => box.Password));
        set
        {
            if (value.Length > length || value.Any(ch => ch is < '0' or > '9')) throw new ArgumentException("숫자만 입력할 수 있습니다.");
            assigning = true;
            try { for (var i = 0; i < length; i++) ((PasswordBox)Children[i]).Password = i < value.Length ? value[i].ToString() : ""; }
            finally { assigning = false; }
            PasswordChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public PinInput() { Orientation = Orientation.Horizontal; HorizontalAlignment = HorizontalAlignment.Center; Build(); }
    public void Clear() => Password = "";
    public void FocusFirst() => ((PasswordBox)Children[0]).Focus();
    private void Build()
    {
        Children.Clear();
        for (var i = 0; i < length; i++)
        {
            var index = i;
            var box = new PasswordBox { Width = 40, Height = 48, MaxLength = 1, FontSize = 22,
                Padding = new Thickness(0, 9, 0, 9), Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(box, $"암호 {i + 1}번째 숫자");
            box.GotKeyboardFocus += (_, _) => box.SelectAll();
            box.PreviewTextInput += (_, e) => e.Handled = e.Text.Any(ch => ch is < '0' or > '9');
            box.PasswordChanged += (_, _) => {
                if (assigning) return;
                if (box.Password.Length == 0) { PasswordChanged?.Invoke(this, EventArgs.Empty); return; }
                if (box.Password.Any(ch => ch is < '0' or > '9')) { box.Clear(); return; }
                if (index + 1 < length) ((PasswordBox)Children[index + 1]).Focus();
                PasswordChanged?.Invoke(this, EventArgs.Empty);
            };
            box.PreviewKeyDown += (_, e) => {
                if (e.Key == Key.Back && box.Password.Length == 0 && index > 0)
                { var previous = (PasswordBox)Children[index - 1]; previous.Clear(); previous.Focus(); e.Handled = true; }
                if (e.Key == Key.Left && index > 0) { ((PasswordBox)Children[index - 1]).Focus(); e.Handled = true; }
                if (e.Key == Key.Right && index + 1 < length) { ((PasswordBox)Children[index + 1]).Focus(); e.Handled = true; }
            };
            DataObject.AddPastingHandler(box, (_, e) => {
                e.CancelCommand();
                if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || text.Length == 0 ||
                    text.Any(ch => ch is < '0' or > '9') || text.Length > length - index) return;
                assigning = true;
                try { for (var n = 0; n < text.Length; n++) ((PasswordBox)Children[index + n]).Password = text[n].ToString(); }
                finally { assigning = false; }
                ((PasswordBox)Children[Math.Min(length - 1, index + text.Length)]).Focus();
                PasswordChanged?.Invoke(this, EventArgs.Empty);
            });
            Children.Add(box);
        }
    }
}
