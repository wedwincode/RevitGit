using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using RevitGit.Application.Diff;
using RevitGit.Domain.Snapshots;
using RevitGit.UI.Compare;
using Xunit;

namespace RevitGit.UI.Tests.Compare
{
    public sealed class CompareViewLayoutTests
    {
        [Fact]
        public void StructuralMaterialScopeChangeCanBeRendered()
        {
            Exception failure = null;
            var completed = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                try
                {
                    var before = Snapshot(ParameterScope.Type);
                    var after = Snapshot(ParameterScope.Instance);
                    var view = new CompareView
                    {
                        DataContext = new CompareViewModel(
                            new FamilyDiffEngine().Compare(before, after),
                            new ComparisonSideViewModel("15.08.2026 23:10", "До бетона"),
                            new ComparisonSideViewModel("15.08.2026 23:42", "Бетон"))
                    };

                    view.Measure(new Size(420, 800));
                    view.Arrange(new Rect(0, 0, 420, 800));
                    view.UpdateLayout();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    completed.Set();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)), "Compare view layout did not complete.");
            Assert.Null(failure);
        }

        [Fact]
        public void StructuralMaterialValueChangeCanBeRendered()
        {
            Exception failure = null;
            var completed = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                try
                {
                    var parameter = new FamilyParameterSnapshot(
                        "builtin:STRUCTURAL_MATERIAL_PARAM",
                        "Structural Material",
                        ParameterDataType.Material,
                        ParameterScope.Type,
                        null);
                    var before = Snapshot(parameter, ParameterValue.Null);
                    var after = Snapshot(parameter, ParameterValue.FromString("Бетон"));
                    var view = new CompareView
                    {
                        DataContext = new CompareViewModel(
                            new FamilyDiffEngine().Compare(before, after),
                            new ComparisonSideViewModel("15.08.2026 23:10", null),
                            new ComparisonSideViewModel("Текущее состояние", null))
                    };

                    view.Measure(new Size(420, 800));
                    view.Arrange(new Rect(0, 0, 420, 800));
                    view.UpdateLayout();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    completed.Set();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            Assert.True(completed.Wait(TimeSpan.FromSeconds(5)), "Compare view layout did not complete.");
            Assert.Null(failure);
        }

        private static FamilySnapshot Snapshot(ParameterScope scope)
        {
            return new FamilySnapshot(
                "Family2",
                "Structural Framing",
                new[]
                {
                    new FamilyParameterSnapshot(
                        "builtin:STRUCTURAL_MATERIAL_PARAM",
                        "Structural Material",
                        ParameterDataType.Material,
                        scope,
                        null)
                },
                new[]
                {
                    new FamilyTypeSnapshot("Family2", new KeyValuePair<string, ParameterValue>[0])
                },
                "geometry");
        }

        private static FamilySnapshot Snapshot(FamilyParameterSnapshot parameter, ParameterValue value)
        {
            return new FamilySnapshot(
                "Family2",
                "Structural Framing",
                new[] { parameter },
                new[]
                {
                    new FamilyTypeSnapshot("Family2", new[]
                    {
                        new KeyValuePair<string, ParameterValue>(parameter.StableKey, value)
                    })
                },
                "geometry");
        }
    }
}
