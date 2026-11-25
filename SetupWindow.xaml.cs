using System.Windows;
using SchoolDesk.Core;

namespace SchoolDesk;

public partial class SetupWindow : Window
{
    public AppSettings Result { get; }

    public SetupWindow(AppSettings s)
    {
        InitializeComponent();
        Result = SettingsStore.Clone(s);
        SchoolBox.Text = s.SchoolName;
        PcBox.Text = s.ComputerName;
    }

    void Ok_Click(object sender, RoutedEventArgs e)
    {
        var err = PasswordService.Validate(P1.Password);
        if (err != null) { Err.Text = err; return; }
        if (P1.Password != P2.Password) { Err.Text = "Пароли не совпадают."; return; }
        Result.SchoolName = SchoolBox.Text.Trim();
        Result.ComputerName = PcBox.Text.Trim();
        Result.PasswordHash = PasswordService.Hash(P1.Password);
        DialogResult = true;
    }
}
