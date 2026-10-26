using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TempChain.Chat;
using TempChain.Chat.Models;
using TempChain.Chat.Services;
class Program {
 [STAThread] static int Main() {
  var root = Path.GetFullPath("artifacts/security-ui-check/run-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  var app = new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
  var ns=System.Xml.Linq.XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml/presentation");
  var doc=System.Xml.Linq.XDocument.Load("desktop/TempChain.Chat/App.xaml");
  var dictionary=new System.Xml.Linq.XElement(ns+"ResourceDictionary",new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns+"x","http://schemas.microsoft.com/winfx/2006/xaml"),doc.Root!.Element(ns+"Application.Resources")!.Nodes());
  app.Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary.ToString());
  var store=new SettingsStore(root); store.Save(new AppSettings { AutoUpdate=false,NotificationDurationSeconds=4,AutoLockMinutes=10 });
  var dialog = new SettingsWindow(store,store.Load());
  ((ComboBox)dialog.FindName("DurationBox")).SelectedIndex=0;
  if(new SettingsStore(root).Load().NotificationDurationSeconds!=3) throw new Exception("selection not saved immediately");
  dialog.Close();
  var reopened=new SettingsWindow(store,new SettingsStore(root).Load());
  if(((ComboBox)reopened.FindName("DurationBox")).SelectedIndex!=0) throw new Exception("duration reset after reopen");
  if(((TextBox)reopened.FindName("AutoLockMinutesBox")).Text!="10") throw new Exception("idle preference lost");
  Render(reopened,Path.Combine(root,"settings.png")); reopened.Close();
  var fixture = AppLockPassword.Create("fixture-user","fixture-nas-password","fixture-lock-password");
  new PersistentLoginStore(root).Save(fixture);
  var main = new MainWindow(store);
  Invoke(main,"RestoreSavedLogin");
  if(((Border)main.FindName("LockOverlay")).Visibility!=Visibility.Visible) throw new Exception("restart did not lock");
  if(((Grid)main.FindName("ChatView")).Visibility!=Visibility.Collapsed) throw new Exception("chat exposed while locked");
  if(((Grid)main.FindName("LoginView")).Visibility!=Visibility.Collapsed) throw new Exception("NAS login should be hidden for saved login");
  Render(main,Path.Combine(root,"lock.png"));
  var field=typeof(MainWindow).GetField("pendingLogin",BindingFlags.Instance|BindingFlags.NonPublic)!;
  field.SetValue(main,((string,string)?)("fixture-user","fixture-nas-password"));
  Invoke(main,"ShowSecurityGate",true);
  if(((StackPanel)main.FindName("LockConfirmPanel")).Visibility!=Visibility.Visible) throw new Exception("setup confirmation absent");
  Render(main,Path.Combine(root,"setup.png"));
  typeof(MainWindow).GetField("client",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,new SynologyChatClient(new Uri("https://fixture.example.test/")));
  ((PasswordBox)main.FindName("AppLockPasswordBox")).Password="fixture-lock-password";
  ((PasswordBox)main.FindName("AppLockConfirmBox")).Password="fixture-lock-password";
  Invoke(main,"Unlock_Click",main,new RoutedEventArgs()); WaitUnlock(main);
  if(((Border)main.FindName("LockOverlay")).Visibility!=Visibility.Collapsed) throw new Exception("setup did not finish");
  Invoke(main,"LockApp");
  ((PasswordBox)main.FindName("AppLockPasswordBox")).Password="wrong-password";
  Invoke(main,"Unlock_Click",main,new RoutedEventArgs()); WaitUnlock(main);
  if(((Border)main.FindName("LockOverlay")).Visibility!=Visibility.Visible) throw new Exception("wrong password unlocked app");
  typeof(MainWindow).GetField("nextUnlockAttempt",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(main,0L);
  ((PasswordBox)main.FindName("AppLockPasswordBox")).Password="fixture-lock-password";
  Invoke(main,"Unlock_Click",main,new RoutedEventArgs()); WaitUnlock(main);
  if(((Border)main.FindName("LockOverlay")).Visibility!=Visibility.Collapsed) throw new Exception("correct password did not unlock");
  Console.WriteLine("PASS WPF: initial password setup persists login; wrong password keeps lock; correct password unlocks without NAS logout.");
  var contact=new ContactItem(new ChatUser(7,"fixture-user","박범준 사원",Email:"fixture@example.test"));
  if(contact.Title!="박범준 사원" || contact.Subtitle!="fixture@example.test") throw new Exception("contact display mismatch");
  Console.WriteLine("PASS WPF: duration selection autosaves; reopen preserves 3 sec; lock time preserved; saved login starts locked; chat and NAS login hidden; setup confirmation present; name/title and email bound.");
  Console.WriteLine("Rendered: "+root);
  main.Close(); app.Shutdown(); return 0;
 }
 static void Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
 static void WaitUnlock(MainWindow window) {
  var field=typeof(MainWindow).GetField("unlocking",BindingFlags.Instance|BindingFlags.NonPublic)!;
  var frame=new DispatcherFrame(); var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(20) }; var until=Environment.TickCount64+15000;
  timer.Tick+=(_,_)=> { if(!(bool)field.GetValue(window)! || Environment.TickCount64>until) { timer.Stop();frame.Continue=false; } };timer.Start();Dispatcher.PushFrame(frame);
  if((bool)field.GetValue(window)!) throw new Exception("unlock operation timed out");
 }
 static void Render(Window window,string path) {
  var surface=(FrameworkElement)window.Content; surface.Measure(new Size(window.Width,window.Height)); surface.Arrange(new Rect(0,0,window.Width,window.Height));surface.UpdateLayout();
  var image=new RenderTargetBitmap((int)window.Width,(int)window.Height,96,96,PixelFormats.Pbgra32);image.Render(surface);
  var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using var file=File.Create(path);png.Save(file);
 }
}
