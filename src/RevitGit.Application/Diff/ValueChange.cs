namespace RevitGit.Application.Diff
{
    public sealed class ValueChange<T>
    {
        internal ValueChange(T oldValue, T newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }

        public T OldValue { get; }

        public T NewValue { get; }
    }
}
