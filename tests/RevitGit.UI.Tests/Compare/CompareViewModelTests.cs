using System;
using System.Collections.Generic;
using System.Linq;
using RevitGit.Application.Diff;
using RevitGit.Domain.Snapshots;
using RevitGit.UI.Compare;
using Xunit;

namespace RevitGit.UI.Tests.Compare
{
    public sealed class CompareViewModelTests
    {
        [Fact]
        public void EmptyDiffShowsDirectionAndClearEmptyState()
        {
            var snapshot = Snapshot();
            var vm = Create(snapshot, snapshot);
            Assert.Equal("15.08.2026 12:30", vm.Before.Label);
            Assert.Equal("Текущее состояние", vm.After.Label);
            Assert.Equal("Изменений нет.", vm.EmptyMessage);
            Assert.False(vm.HasChanges);
        }

        [Fact]
        public void MapsMetadataParametersTypesValuesAndGeometryWithoutTechnicalIds()
        {
            var beforeParameter = Parameter("width", "Ширина", ParameterScope.Type, "Width / 2");
            var afterParameter = Parameter("width", "Ширина", ParameterScope.Instance, "Width / 2 + Offset");
            var removed = Parameter("old", "Старый параметр", ParameterScope.Type, null);
            var added = Parameter("height", "Высота", ParameterScope.Type, null);
            var before = Snapshot("Door", "Двери", new[] { beforeParameter, removed },
                new[] { Type("900x2100", "width", 900) }, "a");
            var after = Snapshot("Door_Main", "Окна", new[] { afterParameter, added },
                new[] { Type("900x2100", "width", 1000), Type("1200x2100", "width", 1200) }, "b");

            var vm = Create(before, after);

            Assert.Equal(2, vm.FamilyChanges.Count);
            Assert.Contains(vm.ParameterChanges, x => x.Name == "Высота" && x.ChangeLabel == "Добавлено");
            Assert.Contains(vm.ParameterChanges, x => x.Name == "Старый параметр" && x.ChangeLabel == "Удалено");
            var modified = vm.ParameterChanges.Single(x => x.Name == "Ширина");
            Assert.Contains(modified.Fields, x => x.Label == "Формула" && x.Before == "Width / 2" && x.After == "Width / 2 + Offset");
            Assert.Contains(modified.Fields, x => x.Label == "Область" && x.Before == "Параметр типа" && x.After == "Параметр экземпляра");
            Assert.Contains(vm.TypeChanges, x => x.Name == "1200x2100" && x.ChangeLabel == "Добавлено");
            Assert.Contains(vm.TypeChanges.Single(x => x.Name == "900x2100").Values,
                x => x.Name == "Ширина" && x.Before == "900 мм" && x.After == "1000 мм");
            Assert.True(vm.GeometryChanged);
            Assert.Equal("Геометрия изменена.", vm.GeometryMessage);
        }

        [Fact]
        public void RemovedTypeAndAddedRemovedValuesUseReadableStates()
        {
            var width = Parameter("width", "Ширина", ParameterScope.Type, null);
            var height = Parameter("height", "Высота", ParameterScope.Type, null);
            var before = Snapshot(parameters: new[] { width, height }, types: new[]
            {
                new FamilyTypeSnapshot("Удаляемый", new[] { new KeyValuePair<string, ParameterValue>("width", ParameterValue.FromDouble(800)) }),
                new FamilyTypeSnapshot("Основной", new[] { new KeyValuePair<string, ParameterValue>("width", ParameterValue.FromDouble(900)) })
            });
            var after = Snapshot(parameters: new[] { width, height }, types: new[]
            {
                new FamilyTypeSnapshot("Основной", new[] { new KeyValuePair<string, ParameterValue>("height", ParameterValue.FromDouble(2100)) })
            });

            var vm = Create(before, after);

            Assert.Contains(vm.TypeChanges, x => x.Name == "Удаляемый" && x.ChangeLabel == "Удалено");
            var values = vm.TypeChanges.Single(x => x.Name == "Основной").Values;
            Assert.Contains(values, x => x.Name == "Высота" && x.ChangeLabel == "Добавлено" && x.Before == null && x.After == "2100 мм");
            Assert.Contains(values, x => x.Name == "Ширина" && x.ChangeLabel == "Удалено" && x.Before == "900 мм" && x.After == null);
        }

        private static CompareViewModel Create(FamilySnapshot before, FamilySnapshot after)
        {
            return new CompareViewModel(new FamilyDiffEngine().Compare(before, after),
                new ComparisonSideViewModel("15.08.2026 12:30", "Начальная версия"),
                new ComparisonSideViewModel("Текущее состояние", null));
        }

        private static FamilyParameterSnapshot Parameter(string key, string name, ParameterScope scope, string formula) =>
            new FamilyParameterSnapshot(key, name, ParameterDataType.Length, scope, formula);

        private static FamilyTypeSnapshot Type(string name, string key, double value) =>
            new FamilyTypeSnapshot(name, new[] { new KeyValuePair<string, ParameterValue>(key, ParameterValue.FromDouble(value)) });

        private static FamilySnapshot Snapshot(string name = "Door", string category = "Двери",
            IEnumerable<FamilyParameterSnapshot> parameters = null, IEnumerable<FamilyTypeSnapshot> types = null,
            string geometry = "a") => new FamilySnapshot(name, category,
                parameters ?? new FamilyParameterSnapshot[0], types ?? new FamilyTypeSnapshot[0], geometry);
    }
}
