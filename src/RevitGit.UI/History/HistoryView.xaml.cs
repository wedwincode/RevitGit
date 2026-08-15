using System;
using System.Windows.Controls;

namespace RevitGit.UI.History
{
    public partial class HistoryView : UserControl
    {
        public HistoryView(HistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }
    }
}
