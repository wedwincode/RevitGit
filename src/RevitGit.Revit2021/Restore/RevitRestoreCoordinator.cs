using System;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitGit.Application.Diff;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Revit2021.Snapshots;

namespace RevitGit.Revit2021.Restore
{
    internal sealed class RevitRestoreCoordinator
    {
        public PendingRevitRestore Begin(
            UIApplication application,
            Document originalDocument,
            RestoreVersionUseCase useCase,
            PreparedRestoreContent prepared)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (originalDocument == null) throw new ArgumentNullException(nameof(originalDocument));
            var originalPath = originalDocument.PathName;
            Document stagedDocument = null;
            var published = false;
            var finalized = false;
            try
            {
                var openStage = Stopwatch.StartNew();
                stagedDocument = application.OpenAndActivateDocument(prepared.PreparedFamilyFilePath).Document;
                openStage.Stop();
                if (!stagedDocument.IsFamilyDocument)
                    throw new InvalidOperationException("Prepared restore is not a family document.");
                if (!originalDocument.Close(false))
                    throw new InvalidOperationException("The current family document could not be closed.");

                useCase.Publish(prepared);
                published = true;
                var result = useCase.Finalize(prepared);
                finalized = true;

                var closeCommand = RevitCommandId.LookupPostableCommandId(PostableCommand.Close);
                if (!application.CanPostCommand(closeCommand))
                    throw new InvalidOperationException("Revit cannot close the staged family document.");
                application.PostCommand(closeCommand);
                Debug.WriteLine("Family History restore stage open: " + openStage.ElapsedMilliseconds + " ms.");
                return new PendingRevitRestore(originalPath, useCase, prepared, result);
            }
            catch
            {
                if (published && !finalized)
                {
                    try
                    {
                        useCase.Rollback(prepared);
                        if (stagedDocument != null)
                        {
                            application.OpenAndActivateDocument(originalPath);
                            stagedDocument.Close(false);
                            stagedDocument = null;
                        }
                    }
                    catch (Exception rollbackException)
                    {
                        Debug.WriteLine("Family History restore rollback failed: " + rollbackException);
                    }
                }
                else if (!published && stagedDocument != null)
                {
                    try
                    {
                        application.OpenAndActivateDocument(originalPath);
                        stagedDocument.Close(false);
                        stagedDocument = null;
                    }
                    catch (Exception recoveryException)
                    {
                        Debug.WriteLine("Family History restore recovery after publish failure failed: " + recoveryException);
                    }
                }
                throw;
            }
            finally { if (!finalized) useCase.Cleanup(prepared); }
        }

        public VersionSummary Complete(UIApplication application, PendingRevitRestore pending)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            try
            {
                var reopened = application.OpenAndActivateDocument(pending.OriginalPath).Document;
                if (pending.Prepared.SourceSnapshot != null)
                {
                    var live = new RevitFamilySnapshotExtractor().Extract(reopened);
                    if (new FamilyDiffEngine().Compare(pending.Prepared.SourceSnapshot, live).HasChanges)
                        throw new InvalidOperationException("The reopened family does not match the restored snapshot.");
                }
                return pending.Version;
            }
            catch (Exception exception)
            {
                throw new RestoreReopenException(pending.OriginalPath, exception);
            }
            finally
            {
                pending.UseCase.Cleanup(pending.Prepared);
            }
        }
    }
}
