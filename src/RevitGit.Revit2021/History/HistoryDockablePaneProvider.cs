using Autodesk.Revit.UI;
using RevitGit.UI.History;

namespace RevitGit.Revit2021.History
{
    internal sealed class HistoryDockablePaneProvider : IDockablePaneProvider
    {
        private readonly HistoryView _view;

        public HistoryDockablePaneProvider(HistoryView view)
        {
            _view = view;
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = _view;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right
            };
        }
    }
}
