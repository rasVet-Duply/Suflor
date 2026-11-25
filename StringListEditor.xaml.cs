using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolDesk;

public partial class StringListEditor : UserControl
{
    public ObservableCollection<string> Items { get; } = new();

    public string Header { get => HeaderText.Text; set => HeaderText.Text = value; }

    public StringListEditor()
    {
        InitializeComponent();
        List.ItemsSource = Items;
    }

    public void Load(IEnumerable<string> source)
    {
        Items.Clear();
        foreach (var s in source) Items.Add(s);
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        var t = Input.Text.Trim();
        if (t.Length > 0 && !Items.Contains(t, StringComparer.OrdinalIgnoreCase)) Items.Add(t);
        Input.Clear();
        Input.Focus();
    }

    void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is string s) Items.Remove(s);
    }

    void Input_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Add_Click(sender, e);
    }
}
