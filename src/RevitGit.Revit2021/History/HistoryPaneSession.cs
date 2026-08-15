using System;
using Autodesk.Revit.UI;

namespace RevitGit.Revit2021.History
{
    internal static class HistoryPaneSession
    {
        private static HistoryPaneCoordinator _coordinator;

        public static void Start(UIControlledApplication application)
        {
            if (_coordinator != null)
            {
                throw new InvalidOperationException("History pane session is already started.");
            }

            _coordinator = new HistoryPaneCoordinator(application);
        }

        public static void Show(UIApplication application)
        {
            RequireCoordinator().Show(application);
        }

        public static void NotifyHistoryChanged()
        {
            _coordinator?.NotifyHistoryChanged();
        }

        public static void Stop()
        {
            _coordinator?.Dispose();
            _coordinator = null;
        }

        private static HistoryPaneCoordinator RequireCoordinator()
        {
            return _coordinator ?? throw new InvalidOperationException("History pane session has not been started.");
        }
    }
}
