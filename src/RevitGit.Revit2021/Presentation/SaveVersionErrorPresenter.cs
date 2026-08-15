using System;
using System.IO;
using RevitGit.Application.Exceptions;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.Git;
using RevitGit.Revit2021.Snapshots;

namespace RevitGit.Revit2021.Presentation
{
    internal static class SaveVersionErrorPresenter
    {
        public static string GetUserMessage(Exception exception)
        {
            if (exception is RepositoryCorruptedException
                || exception is GitRepositoryCorruptedException)
            {
                return "Не удалось открыть историю семейства.\nХранилище повреждено или неполно.";
            }

            var operationException = exception as ApplicationOperationException;
            if (operationException != null)
            {
                if (Contains<RepositoryCorruptedException>(operationException)
                    || Contains<GitRepositoryCorruptedException>(operationException))
                {
                    return "Не удалось открыть историю семейства.\nХранилище повреждено или неполно.";
                }

                if (operationException.Stage == ApplicationFailureStage.SaveDocument)
                {
                    return "Не удалось сохранить семейство Revit. Версия не создана.";
                }

                if (Contains<SnapshotExtractionException>(operationException))
                {
                    return "Не удалось получить данные семейства для версии. Версия не создана.";
                }

                if (Contains<IOException>(operationException)
                    || Contains<UnauthorizedAccessException>(operationException))
                {
                    return "Не удалось прочитать сохранённый файл семейства или записать локальную историю.\n"
                           + "Проверьте доступ к папке и свободное место на диске.";
                }

                return "Не удалось сохранить версию в локальной истории.";
            }

            if (exception is SnapshotExtractionException)
            {
                return "Не удалось получить данные семейства для версии. Версия не создана.";
            }

            if (exception is StorageException || exception is GitStorageException)
            {
                return "Не удалось сохранить версию в локальной истории.";
            }

            return "Не удалось сохранить версию из-за непредвиденной ошибки.";
        }

        private static bool Contains<TException>(Exception exception)
            where TException : Exception
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is TException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
