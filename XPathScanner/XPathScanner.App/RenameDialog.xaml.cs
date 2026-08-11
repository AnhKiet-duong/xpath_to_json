using System.Windows;

namespace XPathScanner.App
{
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; } = "";

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            NewName = NameTextBox.Text.Trim();
            DialogResult = true;
        }
    }
}
