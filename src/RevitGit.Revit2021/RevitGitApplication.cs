using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;

namespace RevitGit.Revit2021
{
    public sealed class RevitGitApplication : IExternalApplication
    {
        private const string RibbonTabName = "История семейств";
        private const string RibbonPanelName = "История";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                CreateRibbonTabIfNeeded(application);
                var panel = GetOrCreateRibbonPanel(application);
                var button = new PushButtonData(
                    "RevitGit.Diagnostic",
                    "Проверка",
                    Assembly.GetExecutingAssembly().Location,
                    typeof(DiagnosticCommand).FullName);

                panel.AddItem(button);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family History failed to initialize: " + exception);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static void CreateRibbonTabIfNeeded(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch (ArgumentException)
            {
                Debug.WriteLine("Family History ribbon tab already exists.");
            }
        }

        private static RibbonPanel GetOrCreateRibbonPanel(UIControlledApplication application)
        {
            var existingPanel = application
                .GetRibbonPanels(RibbonTabName)
                .FirstOrDefault(panel => panel.Name == RibbonPanelName);

            return existingPanel ?? application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
        }
    }
}
