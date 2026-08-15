using System.Windows;

namespace RevitGit.UI.SaveVersion
{
    public partial class SaveVersionDialog : Window
    {
        public SaveVersionDialog()
        {
            InitializeComponent();
            Loaded += (sender, args) => CommentTextBox.Focus();
        }

        public string Comment => CommentTextBox.Text;

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
