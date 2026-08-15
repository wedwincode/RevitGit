using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using RevitGit.Application.Models;
using RevitGit.UI.Common;
using RevitGit.UI.Compare;

namespace RevitGit.UI.History
{
    public sealed class HistoryViewModel : ViewModelBase
    {
        private static readonly IReadOnlyList<HistoryVersionItemViewModel> EmptyVersions =
            new ReadOnlyCollection<HistoryVersionItemViewModel>(new List<HistoryVersionItemViewModel>());

        private readonly IHistoryRefreshRequest _refreshRequest;
        private readonly IHistoryCompareRequest _compareRequest;
        private readonly IHistoryRestoreRequest _restoreRequest;
        private HistoryPaneState _state;
        private string _statusMessage;
        private string _familyFileName;
        private string _currentVariantName;
        private IReadOnlyList<HistoryVersionItemViewModel> _versions;
        private HistoryVersionItemViewModel _selectedVersion;
        private HistoryVersionItemViewModel _selectedComparisonTarget;
        private bool _isBusy;
        private bool _isComparePage;
        private bool _isChoosingTarget;
        private CompareViewModel _comparison;
        private string _compareErrorMessage;
        private readonly RelayCommand _compareWithCurrentCommand;
        private readonly RelayCommand _chooseOtherVersionCommand;
        private readonly RelayCommand _confirmSavedComparisonCommand;
        private readonly RelayCommand _restoreCommand;

        public HistoryViewModel(IHistoryRefreshRequest refreshRequest) : this(refreshRequest, null, null) { }

        public HistoryViewModel(IHistoryRefreshRequest refreshRequest, IHistoryCompareRequest compareRequest, IHistoryRestoreRequest restoreRequest = null)
        {
            _refreshRequest = refreshRequest ?? throw new ArgumentNullException(nameof(refreshRequest));
            _compareRequest = compareRequest;
            _restoreRequest = restoreRequest;
            _versions = EmptyVersions;
            _state = HistoryPaneState.NoDocument;
            _statusMessage = MessageFor(HistoryPaneState.NoDocument);
            RefreshCommand = new RelayCommand(Refresh);
            _compareWithCurrentCommand = new RelayCommand(CompareWithCurrent, CanCompare);
            _chooseOtherVersionCommand = new RelayCommand(ChooseOtherVersion, CanCompare);
            _confirmSavedComparisonCommand = new RelayCommand(ConfirmSavedComparison,
                () => CanCompare() && SelectedComparisonTarget != null);
            CompareWithCurrentCommand = _compareWithCurrentCommand;
            ChooseOtherVersionCommand = _chooseOtherVersionCommand;
            ConfirmSavedComparisonCommand = _confirmSavedComparisonCommand;
            BackToHistoryCommand = new RelayCommand(BackToHistory);
            _restoreCommand = new RelayCommand(Restore, CanRestore);
            RestoreCommand = _restoreCommand;
        }

        public HistoryPaneState State { get => _state; private set => SetProperty(ref _state, value); }
        public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
        public string FamilyFileName { get => _familyFileName; private set => SetProperty(ref _familyFileName, value); }
        public string CurrentVariantName { get => _currentVariantName; private set => SetProperty(ref _currentVariantName, value); }
        public IReadOnlyList<HistoryVersionItemViewModel> Versions { get => _versions; private set => SetProperty(ref _versions, value); }

        public HistoryVersionItemViewModel SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                if (SetProperty(ref _selectedVersion, value))
                {
                    OnPropertyChanged(nameof(RestoreToolTip));
                    RaiseActionCanExecuteChanged();
                }
            }
        }

        public HistoryVersionItemViewModel SelectedComparisonTarget
        {
            get => _selectedComparisonTarget;
            set { if (SetProperty(ref _selectedComparisonTarget, value)) _confirmSavedComparisonCommand.RaiseCanExecuteChanged(); }
        }

        public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseActionCanExecuteChanged(); } }
        public bool IsComparePage { get => _isComparePage; private set { if (SetProperty(ref _isComparePage, value)) OnPropertyChanged(nameof(IsHistoryPage)); } }
        public bool IsHistoryPage => !IsComparePage;
        public bool IsChoosingTarget { get => _isChoosingTarget; private set => SetProperty(ref _isChoosingTarget, value); }
        public CompareViewModel Comparison { get => _comparison; private set => SetProperty(ref _comparison, value); }
        public string CompareErrorMessage { get => _compareErrorMessage; private set => SetProperty(ref _compareErrorMessage, value); }

        public ICommand RefreshCommand { get; }
        public ICommand CompareWithCurrentCommand { get; }
        public ICommand ChooseOtherVersionCommand { get; }
        public ICommand ConfirmSavedComparisonCommand { get; }
        public ICommand BackToHistoryCommand { get; }
        public ICommand RestoreCommand { get; }
        public string RestoreStatusMessage { get; private set; }
        public string RestoreToolTip => SelectedVersion != null && SelectedVersion.IsCurrent
            ? "Эта версия уже текущая."
            : null;

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
            IsComparePage = false;
            IsChoosingTarget = false;
            IsBusy = false;
            RestoreStatusMessage = null;
            OnPropertyChanged(nameof(RestoreStatusMessage));
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

        public void ShowComparison(CompareViewModel comparison)
        {
            Comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));
            CompareErrorMessage = null;
            IsBusy = false;
            IsChoosingTarget = false;
            IsComparePage = true;
        }

        public void ShowCompareError(string message)
        {
            Comparison = null;
            CompareErrorMessage = message;
            IsBusy = false;
            IsChoosingTarget = false;
            IsComparePage = true;
        }

        private bool CanCompare() => _compareRequest != null && State == HistoryPaneState.Ready && SelectedVersion != null && !IsBusy;
        private void CompareWithCurrent()
        {
            IsBusy = true;
            _compareRequest.RequestCompareWithCurrent(SelectedVersion.Id);
        }
        private void ChooseOtherVersion()
        {
            IsChoosingTarget = true;
            SelectedComparisonTarget = Versions.FirstOrDefault(item => !item.Id.Equals(SelectedVersion.Id));
        }
        private void ConfirmSavedComparison()
        {
            IsBusy = true;
            _compareRequest.RequestCompareSaved(SelectedVersion.Id, SelectedComparisonTarget.Id);
        }
        private void BackToHistory()
        {
            IsComparePage = false;
            CompareErrorMessage = null;
            Comparison = null;
        }
        private void RaiseCompareCanExecuteChanged()
        {
            _compareWithCurrentCommand?.RaiseCanExecuteChanged();
            _chooseOtherVersionCommand?.RaiseCanExecuteChanged();
            _confirmSavedComparisonCommand?.RaiseCanExecuteChanged();
        }

        private bool CanRestore()
        {
            return _restoreRequest != null && State == HistoryPaneState.Ready
                   && SelectedVersion != null && !SelectedVersion.IsCurrent && !IsBusy;
        }

        private void Restore()
        {
            IsBusy = true;
            RestoreStatusMessage = "Восстановление…";
            OnPropertyChanged(nameof(RestoreStatusMessage));
            _restoreRequest.RequestRestore(SelectedVersion.Id);
        }

        public void ShowRestoreCancelled()
        {
            IsBusy = false;
            RestoreStatusMessage = null;
            OnPropertyChanged(nameof(RestoreStatusMessage));
        }

        public void ShowRestoreError(string message)
        {
            IsBusy = false;
            RestoreStatusMessage = message;
            OnPropertyChanged(nameof(RestoreStatusMessage));
        }

        private void RaiseActionCanExecuteChanged()
        {
            RaiseCompareCanExecuteChanged();
            _restoreCommand?.RaiseCanExecuteChanged();
        }

        private void ResetContent()
        {
            FamilyFileName = null;
            CurrentVariantName = null;
            Versions = EmptyVersions;
            SelectedVersion = null;
            SelectedComparisonTarget = null;
            IsComparePage = false;
            IsChoosingTarget = false;
            IsBusy = false;
            RestoreStatusMessage = null;
            OnPropertyChanged(nameof(RestoreStatusMessage));
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
