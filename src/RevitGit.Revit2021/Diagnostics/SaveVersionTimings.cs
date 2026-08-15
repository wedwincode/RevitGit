using System;
using System.Diagnostics;

namespace RevitGit.Revit2021.Diagnostics
{
    public sealed class SaveVersionTimings
    {
        public long DocumentSaveMilliseconds { get; private set; }

        public long SnapshotMilliseconds { get; private set; }

        public long StorageMilliseconds { get; private set; }

        public long TotalMilliseconds { get; private set; }

        public void MeasureDocumentSave(Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                stopwatch.Stop();
                DocumentSaveMilliseconds = stopwatch.ElapsedMilliseconds;
            }
        }

        public T MeasureSnapshot<T>(Func<T> action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return action();
            }
            finally
            {
                stopwatch.Stop();
                SnapshotMilliseconds = stopwatch.ElapsedMilliseconds;
            }
        }

        public void Complete(long totalMilliseconds)
        {
            TotalMilliseconds = totalMilliseconds;
            StorageMilliseconds = Math.Max(0, totalMilliseconds - DocumentSaveMilliseconds - SnapshotMilliseconds);
        }

        public void WriteToDebug()
        {
            Debug.WriteLine("SaveVersion:");
            Debug.WriteLine("Document.Save: " + DocumentSaveMilliseconds + " ms");
            Debug.WriteLine("Snapshot: " + SnapshotMilliseconds + " ms");
            Debug.WriteLine("Storage: " + StorageMilliseconds + " ms");
            Debug.WriteLine("Total: " + TotalMilliseconds + " ms");
        }
    }
}
