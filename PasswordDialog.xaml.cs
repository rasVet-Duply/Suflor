using System.Windows;
using SchoolDesk.Core;

namespace SchoolDesk;

public partial class PasswordDialog : Window
{
    readonly string _hash;

    public PasswordDialog(string title, string hash)
    {
        InitializeComponent();
        Title = title;
        Head.Text = title;
        _hash = hash;
        Loaded += (_, _) => Pwd.Focus();
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Lockout.IsLocked(out var left))
        {
            Err.Text = $"Слишком много попыток. Подождите {Math.Ceiling(left.TotalSeconds)} с.";
            return;
        }
        var ok = PasswordService.Verify(Pwd.Password, _hash);
        Lockout.Register(ok);
        if (ok) { Log.Info("Пароль принят"); DialogResult = true; return; }
        Log.Warn("Неверный пароль");
        Err.Text = "Неверный пароль";
        Pwd.Clear();
        Pwd.Focus();
    }
}
