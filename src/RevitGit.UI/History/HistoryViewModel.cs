using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using RevitGit.Application.Models;
using RevitGit.UI.Common;

namespace RevitGit.UI.History
{
    public sealed class HistoryViewModel : ViewModelBase
    {
        private static readonly IReadOnlyList<HistoryVersionItemViewModel> EmptyVersions =
            new ReadOnlyCollection<HistoryVersionItemViewModel>(new List<HistoryVersionItemViewModel>());

        private readonly IHistoryRefreshRequest _refreshRequest;
        private HistoryPaneState _state;
        private string _statusMessage;
        private string _familyFileName;
        private string _currentVariantName;
        private IReadOnlyList<HistoryVersionItemViewModel> _versions;
        private HistoryVersionItemViewModel _selectedVersion;

        public HistoryViewModel(IHistoryRefreshRequest refreshRequest)
        {
            _refreshRequest = refreshRequest ?? throw new ArgumentNullException(nameof(refreshRequest));
            _versions = EmptyVersions;
            _state = HistoryPaneState.NoDocument;
            _statusMessage = MessageFor(HistoryPaneState.NoDocument);
            RefreshCommand = new RelayCommand(Refresh);
        }

        public HistoryPaneState State { get => _state; private set => SetProperty(ref _state, value); }
        public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
        public string FamilyFileName { get => _familyFileName; private set => SetProperty(ref _familyFileName, value); }
        public string CurrentVariantName { get => _currentVariantName; private set => SetProperty(ref _currentVariantName, value); }
        public IReadOnlyList<HistoryVersionItemViewModel> Versions { get => _versions; private set => SetProperty(ref _versions, value); }

        public HistoryVersionItemViewModel SelectedVersion
        {
            get => _selectedVersion;
            set => SetProperty(ref _selectedVersion, value);
        }

        public ICommand RefreshCommand { get; }

        public void BeginRefresh()
        {
            State = HistoryPaneState.Loading;
            StatusMessage = MessageFor(HistoryPaneState.Loading);
        }

        public void ShowContextState(HistoryPaneState state)
        {
            if (state != HistoryPaneState.NoDocument
                && state != HistoryPaneState.NotFamily
                && state != HistoryPaneState.UnsavedFamily)
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            ResetContent();
            State = state;
            StatusMessage = MessageFor(state);
        }

        public void ShowNoHistory(string familyFileName)
        {
            ResetContent();
            FamilyFileName = familyFileName;
            State = HistoryPaneState.NoHistory;
            StatusMessage = MessageFor(HistoryPaneState.NoHistory);
        }

        public void ShowHistory(
            string familyFileName,
            string currentVariantName,
            IEnumerable<VersionSummary> versions)
        {
            if (versions == null)
            {
                throw new ArgumentNullException(nameof(versions));
            }

            var items = versions.Select(version => new HistoryVersionItemViewModel(version)).ToList();
            Versions = new ReadOnlyCollection<HistoryVersionItemViewModel>(items);
            FamilyFileName = familyFileName;
            CurrentVariantName = currentVariantName;
            StatusMessage = null;
            State = HistoryPaneState.Ready;
            SelectedVersion = items.FirstOrDefault(item => item.IsCurrent) ?? items.FirstOrDefault();
        }

        public void ShowError(bool corrupted)
        {
            ResetContent();
            State = HistoryPaneState.Error;
            StatusMessage = corrupted
                ? "История повреждена или неполна."
                : "Не удалось загрузить историю семейства.";
        }

        private void Refresh()
        {
            BeginRefresh();
            _refreshRequest.RequestRefresh();
        }

        private void ResetContent()
        {
            FamilyFileName = null;
            CurrentVariantName = null;
            Versions = EmptyVersions;
            SelectedVersion = null;
        }

        private static string MessageFor(HistoryPaneState state)
        {
            switch (state)
            {
                case HistoryPaneState.Loading:
                    return "Загрузка истории…";
                case HistoryPaneState.NoDocument:
                    return "Откройте семейство Revit, чтобы посмотреть историю.";
                case HistoryPaneState.NotFamily:
                    return "История доступна только для семейств Revit (.rfa).";
                case HistoryPaneState.UnsavedFamily:
                    return "Сначала сохраните семейство как .rfa.";
                case HistoryPaneState.NoHistory:
                    return "Для этого семейства ещё нет сохранённых версий.";
                default:
                    return null;
            }
        }
    }
}
