using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RevitGit.Application.Diff;
using RevitGit.Domain.Snapshots;

namespace RevitGit.UI.Compare
{
    public sealed class CompareViewModel
    {
        private readonly ParameterValueFormatter _formatter = new ParameterValueFormatter();

        public CompareViewModel(FamilyDiff diff, ComparisonSideViewModel before, ComparisonSideViewModel after)
        {
            if (diff == null) throw new ArgumentNullException(nameof(diff));
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
            FamilyChanges = ReadOnly(MapFamily(diff));
            ParameterChanges = ReadOnly(MapParameters(diff.ParameterChanges));
            TypeChanges = ReadOnly(MapTypes(diff.TypeChanges));
            GeometryChanged = diff.GeometryChanged;
            GeometryMessage = GeometryChanged ? "Геометрия изменена." : null;
            HasChanges = diff.HasChanges;
            EmptyMessage = HasChanges ? null : "Изменений нет.";
            Summary = "Параметры: " + ParameterChanges.Count + "   Типы: " + TypeChanges.Count
                      + (GeometryChanged ? "   Геометрия: изменена" : string.Empty);
        }

        public ComparisonSideViewModel Before { get; }
        public ComparisonSideViewModel After { get; }
        public IReadOnlyList<CompareFieldViewModel> FamilyChanges { get; }
        public IReadOnlyList<ParameterChangeViewModel> ParameterChanges { get; }
        public IReadOnlyList<FamilyTypeChangeViewModel> TypeChanges { get; }
        public bool HasFamilyChanges => FamilyChanges.Count > 0;
        public bool HasParameterChanges => ParameterChanges.Count > 0;
        public bool HasTypeChanges => TypeChanges.Count > 0;
        public bool GeometryChanged { get; }
        public string GeometryMessage { get; }
        public bool HasChanges { get; }
        public string EmptyMessage { get; }
        public string Summary { get; }

        private static List<CompareFieldViewModel> MapFamily(FamilyDiff diff)
        {
            var result = new List<CompareFieldViewModel>();
            if (diff.FamilyNameChange != null) result.Add(new CompareFieldViewModel("Имя", diff.FamilyNameChange.OldValue, diff.FamilyNameChange.NewValue));
            if (diff.CategoryChange != null) result.Add(new CompareFieldViewModel("Категория", diff.CategoryChange.OldValue, diff.CategoryChange.NewValue));
            return result;
        }

        private static List<ParameterChangeViewModel> MapParameters(IEnumerable<ParameterChange> changes)
        {
            var result = new List<ParameterChangeViewModel>();
            foreach (var change in changes)
            {
                var fields = new List<CompareFieldViewModel>();
                if (change.Kind == ChangeKind.Added || change.Kind == ChangeKind.Removed)
                {
                    var state = change.After ?? change.Before;
                    fields.Add(new CompareFieldViewModel("Тип данных", null, DataType(state.DataType)));
                    fields.Add(new CompareFieldViewModel("Область", null, Scope(state.Scope)));
                    if (state.Formula != null) fields.Add(new CompareFieldViewModel("Формула", null, state.Formula));
                }
                else
                {
                    if (change.NameChange != null) fields.Add(new CompareFieldViewModel("Имя", change.NameChange.OldValue, change.NameChange.NewValue));
                    if (change.DataTypeChange != null) fields.Add(new CompareFieldViewModel("Тип данных", DataType(change.DataTypeChange.OldValue), DataType(change.DataTypeChange.NewValue)));
                    if (change.ScopeChange != null) fields.Add(new CompareFieldViewModel("Область", Scope(change.ScopeChange.OldValue), Scope(change.ScopeChange.NewValue)));
                    if (change.FormulaChange != null) fields.Add(new CompareFieldViewModel("Формула", Display(change.FormulaChange.OldValue), Display(change.FormulaChange.NewValue)));
                }
                result.Add(new ParameterChangeViewModel(change.Name, Kind(change.Kind), ReadOnly(fields)));
            }
            return result;
        }

        private List<FamilyTypeChangeViewModel> MapTypes(IEnumerable<FamilyTypeChange> changes)
        {
            var result = new List<FamilyTypeChangeViewModel>();
            foreach (var change in changes)
            {
                var values = new List<ParameterValueChangeViewModel>();
                foreach (var value in change.ValueChanges)
                {
                    values.Add(new ParameterValueChangeViewModel(value.ParameterName, Kind(value.Kind),
                        value.Before == null ? null : _formatter.Format(value.Before, value.ParameterDataType),
                        value.After == null ? null : _formatter.Format(value.After, value.ParameterDataType)));
                }
                result.Add(new FamilyTypeChangeViewModel(change.Name, Kind(change.Kind), ReadOnly(values)));
            }
            return result;
        }

        private static string Display(string value) => value ?? "не задано";
        private static string Kind(ChangeKind kind) => kind == ChangeKind.Added ? "Добавлено" : kind == ChangeKind.Removed ? "Удалено" : "Изменено";
        private static string Scope(ParameterScope scope) => scope == ParameterScope.Instance ? "Параметр экземпляра" : "Параметр типа";
        private static string DataType(ParameterDataType type)
        {
            switch (type)
            {
                case ParameterDataType.Text: return "Текст";
                case ParameterDataType.Integer: return "Целое число";
                case ParameterDataType.Number: return "Число";
                case ParameterDataType.Length: return "Длина";
                case ParameterDataType.Area: return "Площадь";
                case ParameterDataType.Volume: return "Объём";
                case ParameterDataType.Angle: return "Угол";
                case ParameterDataType.YesNo: return "Да/Нет";
                case ParameterDataType.Material: return "Материал";
                case ParameterDataType.FamilyType: return "Тип семейства";
                default: return "Неизвестный тип";
            }
        }
        private static IReadOnlyList<T> ReadOnly<T>(IList<T> items) => new ReadOnlyCollection<T>(items);
    }
}
