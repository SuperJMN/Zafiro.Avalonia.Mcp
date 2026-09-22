using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;
using Zafiro.Avalonia.Mcp.AppHost.Handlers;
using Zafiro.Avalonia.Mcp.AppHost.Provenance;
using Zafiro.Avalonia.Mcp.Protocol.Messages;
using Zafiro.Avalonia.Mcp.Protocol.Models;

namespace Zafiro.Avalonia.Mcp.Tests.Handlers;

[Collection("Avalonia")]
public class PropertyProvenanceTests
{
    public PropertyProvenanceTests(AvaloniaTestFixture _)
    {
        NodeRegistry.Clear();
    }

    [Fact]
    public void EqualSetterValues_IdentifyTheActualWinningStyle_AndShareIdsAcrossTools()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var border = new Border();
            var earlier = WidthStyle(42);
            var later = WidthStyle(42);
            using var scope = new ControlScope(border, earlier, later);

            var explanation = Explain(border, Border.WidthProperty, candidates: true);
            var styles = Styles(border);

            var winning = Assert.Single(styles.Styles, style => style.Setters.Any(setter => setter.Wins == true));
            Assert.Equal(winning.SourceId, explanation.Origin!.SourceId);
            Assert.Equal(styles.Styles.Max(style => style.FrameOrder), winning.FrameOrder);
            Assert.Equal(NodeRegistry.GetOrRegister(scope.Window), winning.Scope!.NodeId);
            Assert.Single(explanation.Candidates!, candidate => candidate.Wins == true);
            Assert.Contains(explanation.Candidates!, candidate => candidate.Reason == "lowerRuntimeFrameOrder");
            Assert.Equal(explanation.Property.Id, winning.Setters.Single().Property.Id);
            Assert.Equal("42", explanation.EffectiveValue);
        });
    }

    [Fact]
    public void ALocalValue_WinsOverATemplateEntry_WithoutInventingAnAxamlLocation()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Border? child = null;
            var control = new ContentControl
            {
                Template = new FuncControlTemplate<ContentControl>((_, _) =>
                {
                    child = new Border { Width = 60 };
                    child.SetValue(Border.WidthProperty, double.NaN, BindingPriority.Template);
                    return child;
                })
            };
            using var scope = new ControlScope(control);

            var explanation = Explain(child!, Border.WidthProperty, candidates: true);

            Assert.Equal("60", explanation.EffectiveValue);
            Assert.Equal("LocalValue", explanation.Priority);
            Assert.Equal("localValue", explanation.Origin!.Kind);
            var template = Assert.Single(explanation.Candidates!);
            Assert.Equal("Template", template.Priority);
            Assert.Equal("lowerPriority", template.Reason);
            Assert.Equal(NodeRegistry.GetOrRegister(control), template.Origin.TemplatedParent!.NodeId);
            Assert.Contains(explanation.Resolution.Unavailable, item => item.Evidence == "originatingTemplate");
            Assert.Null(template.Origin.Location);
        });
    }

    [Fact]
    public void ClassesAndPseudoClasses_AreCapturedWithRealAttachedTriggerState()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new ProbeControl { IsEnabled = false };
            control.Classes.Add("warning");
            var trigger = new Style(selector => selector.OfType<ProbeControl>().Class("warning").Class(":alert"));
            trigger.Setters.Add(new Setter(Border.WidthProperty, 81d));
            using var scope = new ControlScope(control, trigger);

            var inactive = Styles(control, includeInactive: true);
            Assert.False(Assert.Single(inactive.Styles).Active);
            Assert.Empty(Styles(control).Styles);
            Assert.Contains(":disabled", inactive.ClassState.PseudoClasses);
            Assert.Equal(["warning"], inactive.ClassState.Classes);

            control.SetAlert(true);
            var active = Styles(control);
            Assert.True(Assert.Single(active.Styles).Active);
            Assert.Contains(":alert", active.ClassState.PseudoClasses);
            Assert.Equal(inactive.Styles[0].SourceId, active.Styles[0].SourceId);
            var explanation = Explain(control, Border.WidthProperty);
            Assert.Equal(active.ClassState.Classes, explanation.ClassState.Classes);
            Assert.Equal(active.ClassState.PseudoClasses, explanation.ClassState.PseudoClasses);

            control.Classes.Remove("warning");
            Assert.False(Assert.Single(Styles(control, includeInactive: true).Styles).Active);
            Assert.Contains(":alert", control.Classes);
        });
    }

    [Fact]
    public void MoreActivators_DoNotOutrankALaterStyleAtTheSamePriority()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new ProbeControl();
            control.Classes.Add("first");
            control.Classes.Add("second");
            control.SetAlert(true);
            var complex = new Style(selector => selector.OfType<ProbeControl>().Class("first").Class("second").Class(":alert"));
            complex.Setters.Add(new Setter(Border.WidthProperty, 10d));
            var simple = new Style(selector => selector.OfType<ProbeControl>().Class("first"));
            simple.Setters.Add(new Setter(Border.WidthProperty, 20d));
            using var scope = new ControlScope(control, complex, simple);

            var explanation = Explain(control, Border.WidthProperty, candidates: true);

            Assert.Equal("20", explanation.EffectiveValue);
            Assert.Equal("StyleTrigger", explanation.Priority);
            Assert.Equal("ProbeControl.first", explanation.Origin!.Selector);
            Assert.All(explanation.Candidates!, candidate => Assert.Equal("StyleTrigger", candidate.Priority));
        });
    }

    [Fact]
    public void NestedControlThemes_RetainParentAndBasedOnSourceIdentity()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            control.Classes.Add("selected");
            var baseTheme = new ControlTheme(typeof(Border));
            baseTheme.Setters.Add(new Setter(Border.WidthProperty, 30d));
            var theme = new ControlTheme(typeof(Border)) { BasedOn = baseTheme };
            theme.Setters.Add(new Setter(Border.HeightProperty, 40d));
            var nested = new Style(selector => selector.Nesting().Class("selected"));
            nested.Setters.Add(new Setter(Border.WidthProperty, 50d));
            theme.Children.Add(nested);
            control.Theme = theme;
            using var scope = new ControlScope(control);

            var styles = Styles(control);
            var derived = Assert.Single(styles.Styles, style => style.BasedOnSourceId is not null);
            Assert.Contains(styles.Styles, style => style.SourceId == derived.BasedOnSourceId);
            var child = Assert.Single(styles.Styles, style => style.ParentSourceId is not null);
            Assert.Equal(derived.SourceId, child.ParentSourceId);
            Assert.Equal(child.SourceId, Explain(control, Border.WidthProperty).Origin!.SourceId);
        });
    }

    [Fact]
    public void Inheritance_FollowsThePropertyParent_NotTheLogicalParent()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var supplier = new ProbeControl();
            supplier.SetValue(ProbeControl.InheritedValueProperty, 19);
            var control = new ProbeControl();
            using var scope = new ControlScope(control);
            scope.Window.SetValue(ProbeControl.InheritedValueProperty, 41);
            control.InheritFrom(supplier);

            var explanation = Explain(control, ProbeControl.InheritedValueProperty);

            Assert.Equal("19", explanation.EffectiveValue);
            Assert.Equal("Inherited", explanation.Priority);
            Assert.Equal(NodeRegistry.GetOrRegister(supplier), explanation.Origin!.InheritedFrom!.Target.NodeId);
            Assert.Equal("localValue", explanation.Origin.InheritedFrom.Origin!.Kind);
            Assert.Equal(explanation.Timestamp, explanation.Origin.InheritedFrom.Timestamp);
        });
    }

    [Fact]
    public void InheritanceDepth_IsBoundedAndReported()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var supplier = new ProbeControl();
            supplier.SetValue(ProbeControl.InheritedValueProperty, 19);
            var control = new ProbeControl();
            control.InheritFrom(supplier);

            var explanation = Explain(control, ProbeControl.InheritedValueProperty, depth: 1);

            Assert.Equal("19", explanation.EffectiveValue);
            Assert.True(explanation.Resolution.Truncated);
            Assert.Contains(explanation.Resolution.Unavailable, item => item.Evidence == "inheritanceDepth");
        });
    }

    [Fact]
    public void Animation_ReportsTheBaseValueAndItsSource()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border { Width = 50 };
            using var scope = new ControlScope(control);
            using var animation = control.Bind(Border.WidthProperty, new ConstantObservable<double>(75), BindingPriority.Animation);

            var explanation = Explain(control, Border.WidthProperty, candidates: true);

            Assert.Equal("75", explanation.EffectiveValue);
            Assert.Equal("Animation", explanation.Priority);
            Assert.Equal("animation", explanation.Origin!.Kind);
            Assert.Equal("50", explanation.BaseValue!.Value);
            Assert.Equal("LocalValue", explanation.BaseValue.Priority);
            Assert.Equal("localValue", explanation.BaseValue.Origin!.Kind);
            Assert.Contains(explanation.Resolution.Unavailable, item => item.Evidence == "animationTimeline");
        });
    }

    [Fact]
    public void AnimationWithoutAnOwnBaseEntry_FollowsInheritedValuesRatherThanTheMetadataDefault()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var supplier = new ProbeControl();
            supplier.SetValue(ProbeControl.InheritedValueProperty, 19);
            var control = new ProbeControl();
            control.InheritFrom(supplier);
            using var animation = control.Bind(ProbeControl.InheritedValueProperty,
                new ConstantObservable<int>(75), BindingPriority.Animation);

            var explanation = Explain(control, ProbeControl.InheritedValueProperty);

            Assert.Equal("75", explanation.EffectiveValue);
            Assert.Equal("19", explanation.BaseValue!.Value);
            Assert.Equal("Inherited", explanation.BaseValue.Priority);
            Assert.Equal("inherited", explanation.BaseValue.Origin!.Kind);
            Assert.Equal(NodeRegistry.GetOrRegister(supplier), explanation.BaseValue.Origin.InheritedFrom!.Target.NodeId);
        });
    }

    [Fact]
    public void ALocalBinding_RetainsItsPathAndDoesNotBecomeALiteral()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var source = new CountingSource();
            var control = new Border();
            using var binding = control.Bind(Border.WidthProperty, new Binding(nameof(CountingSource.Width)) { Source = source });
            var reads = source.ReadCount;

            var explanation = Explain(control, Border.WidthProperty);

            Assert.Equal("42", explanation.EffectiveValue);
            Assert.Equal("localBinding", explanation.Origin!.Kind);
            Assert.Equal("binding", explanation.Origin.Provider.Kind);
            Assert.Contains(nameof(CountingSource.Width), explanation.Origin.Provider.Path);
            Assert.Equal(reads, source.ReadCount);
            Assert.NotNull(explanation.Origin.Provider.Source);
        });
    }

    [Fact]
    public void BindingErrors_ExposeFallbackEvidenceWithoutGuessingItsUseFromEqualValues()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            using var binding = control.Bind(Border.WidthProperty,
                new Binding("Missing") { Source = new object(), FallbackValue = 27d });

            var explanation = Explain(control, Border.WidthProperty);

            Assert.Equal("27", explanation.EffectiveValue);
            Assert.Equal("binding", explanation.Origin!.Provider.Kind);
            Assert.Equal("27", explanation.Origin.Provider.FallbackValue);
            Assert.NotEqual("None", explanation.Origin.Provider.ErrorType);
            Assert.Contains(explanation.Resolution.Unavailable, item => item.Evidence == "fallbackUsage");
        });
    }

    [Fact]
    public void AnUnsetBinding_IsReportedAsACompetingEntry_NotAsTheDefaultProvider()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            using var binding = control.Bind(Border.WidthProperty, new Binding("Missing") { Source = new object() });

            var explanation = Explain(control, Border.WidthProperty, candidates: true);

            Assert.Equal("metadataDefault", explanation.Origin!.Kind);
            var candidate = Assert.Single(explanation.Candidates!);
            Assert.Equal("LocalValue", candidate.Priority);
            Assert.False(candidate.Wins);
            Assert.Equal("binding", candidate.Origin.Provider.Kind);
            Assert.Equal("unsetEntry", candidate.Reason);
        });
    }

    [Fact]
    public void Transitions_ExposeTheAnimationOverrideAndNewBaseValue()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border { Width = 50 };
            using var scope = new ControlScope(control);
            control.Transitions = new Transitions
            {
                new DoubleTransition { Property = Border.WidthProperty, Duration = TimeSpan.FromSeconds(10) }
            };
            control.Width = 90;

            var explanation = Explain(control, Border.WidthProperty);

            Assert.Equal("Animation", explanation.Priority);
            Assert.Equal("90", explanation.BaseValue!.Value);
            Assert.Equal("localValue", explanation.BaseValue.Origin!.Kind);
            Assert.Contains(explanation.Resolution.Unavailable, item => item.Evidence == "animationTimeline");
        });
    }

    [Fact]
    public void ACloserStyleScope_WinsWithinTheSamePriority()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            var parent = new StackPanel { Children = { control } };
            parent.Styles.Add(WidthStyle(60));
            using var scope = new ControlScope(parent, WidthStyle(30));

            var explanation = Explain(control, Border.WidthProperty, candidates: true);

            Assert.Equal("60", explanation.EffectiveValue);
            Assert.Equal(NodeRegistry.GetOrRegister(parent), explanation.Origin!.Scope!.NodeId);
            Assert.Contains(explanation.Candidates!, candidate => candidate.Origin.Scope?.NodeId == NodeRegistry.GetOrRegister(scope.Window));
        });
    }

    [Fact]
    public void ResourceLookup_UsesReverseMergedOrder_WithoutEvaluatingOtherDeferredResources()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            var first = new ResourceDictionary { ["Accent"] = Brushes.Red };
            var last = new ResourceDictionary { ["Accent"] = Brushes.Blue };
            var resources = new ResourceDictionary();
            control.Resources = resources;
            var evaluations = 0;
            resources.AddDeferred("Unused", _ => { evaluations++; return Brushes.Green; });
            control.Resources.MergedDictionaries.Add(first);
            control.Resources.MergedDictionaries.Add(last);
            using var scope = new ControlScope(control);
            using var binding = control.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Accent"));

            var explanation = Explain(control, Border.BackgroundProperty);

            Assert.Equal("#ff0000ff", explanation.EffectiveValue);
            Assert.Equal(ProvenanceIdentity.Id(last), explanation.Origin!.Provider.Resource!.Dictionary!.SourceId);
            Assert.Equal(0, evaluations);
        });
    }

    [Fact]
    public void CoercedDefaults_KeepTheirMetadataOriginAndUncoercedValue()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new ProbeControl();
            control.CoerceValue(ProbeControl.CoercedDefaultProperty);

            var explanation = Explain(control, ProbeControl.CoercedDefaultProperty);

            Assert.Equal("5", explanation.EffectiveValue);
            Assert.Equal("20", explanation.DefaultValue);
            Assert.True(explanation.IsCoercedDefault);
            Assert.Equal("metadataDefault", explanation.Origin!.Kind);
        });
    }

    [Fact]
    public void Inspection_DoesNotStartAnOverriddenBinding()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var source = new CountingSource();
            var control = new Border { Width = 99 };
            var style = new Style(selector => selector.OfType<Border>());
            style.Setters.Add(new Setter(Border.WidthProperty,
                new Binding(nameof(CountingSource.Width)) { Source = source }));
            using var scope = new ControlScope(control, style);
            var reads = source.ReadCount;

            var styles = Styles(control);
            var explanation = Explain(control, Border.WidthProperty, candidates: true);

            Assert.Equal(reads, source.ReadCount);
            Assert.Equal("binding", styles.Styles.Single().Setters.Single().Provider.Kind);
            Assert.False(styles.Styles.Single().Setters.Single().Wins);
            Assert.Equal("99", explanation.EffectiveValue);
        });
    }

    [Fact]
    public void TemplateBinding_ReportsItsSourcePropertyAndTemplatedParent()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Border? child = null;
            var control = new ContentControl
            {
                Width = 62,
                Template = new FuncControlTemplate<ContentControl>((_, _) =>
                {
                    child = new Border();
                    child[!Border.WidthProperty] = new TemplateBinding(ContentControl.WidthProperty);
                    return child;
                })
            };
            using var scope = new ControlScope(control);

            var explanation = Explain(child!, Border.WidthProperty);

            Assert.Equal("templateBinding", explanation.Origin!.Provider.Kind);
            Assert.Equal("Width", explanation.Origin.Provider.SourceProperty!.Name);
            Assert.Equal(NodeRegistry.GetOrRegister(control), explanation.Origin.Provider.Source!.NodeId);
            Assert.Equal("62", explanation.EffectiveValue);
        });
    }

    [Fact]
    public void DynamicResources_ReportTheSupplyingOverrideAndThemeVariant()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            var dictionary = new ResourceDictionary { ["Accent"] = Brushes.Lime };
            control.Resources.ThemeDictionaries[ThemeVariant.Dark] = dictionary;
            using var scope = new ControlScope(control);
            scope.Window.RequestedThemeVariant = ThemeVariant.Dark;
            scope.Window.Resources["Accent"] = Brushes.Red;
            using var binding = control.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Accent"));

            var explanation = Explain(control, Border.BackgroundProperty);

            Assert.Equal("#ff00ff00", explanation.EffectiveValue);
            Assert.Equal("dynamicResource", explanation.Origin!.Provider.Kind);
            Assert.Equal("Accent", explanation.Origin.Provider.Key);
            Assert.Equal("Dark", explanation.Origin.Provider.Resource!.ThemeVariant);
            Assert.NotNull(explanation.Origin.Provider.Resource.Dictionary);
            Assert.Equal(ProvenanceIdentity.Id(dictionary), explanation.Origin.Provider.Resource.Dictionary.SourceId);
            Assert.Equal(typeof(ResourceDictionary).FullName, explanation.Origin.Provider.Resource.Dictionary.Type);
            var firstDictionary = explanation.Origin.Provider.Resource.Dictionary.SourceId;

            control.Resources["Accent"] = Brushes.Blue;
            var changed = Explain(control, Border.BackgroundProperty);
            Assert.Equal("#ff0000ff", changed.EffectiveValue);
            Assert.NotEqual(firstDictionary, changed.Origin!.Provider.Resource!.Dictionary!.SourceId);
        });
    }

    [Fact]
    public void SetCurrentValue_PreservesTheStyleSourceAndPriority()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            using var scope = new ControlScope(control, WidthStyle(40));
            control.SetCurrentValue(Border.WidthProperty, 66d);

            var explanation = Explain(control, Border.WidthProperty);

            Assert.True(explanation.IsCurrentValueOverride);
            Assert.Equal("Style", explanation.Priority);
            Assert.Equal("66", explanation.EffectiveValue);
            Assert.Equal("styleSetter", explanation.Origin!.Kind);
            Assert.Equal("40", explanation.Origin.Provider.Value);
        });
    }

    [Fact]
    public void DefaultsAndCoercion_AreReportedSeparatelyFromTheProvidedValue()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new ProbeControl();
            var defaultValue = Explain(control, ProbeControl.CappedProperty);
            Assert.Equal("metadataDefault", defaultValue.Origin!.Kind);
            Assert.Equal("1", defaultValue.DefaultValue);
            Assert.Equal("Unset", defaultValue.Priority);

            control.SetValue(ProbeControl.CappedProperty, 100);
            var coerced = Explain(control, ProbeControl.CappedProperty);
            Assert.Equal("10", coerced.EffectiveValue);
            Assert.True(coerced.HasCoercion);
            Assert.Equal("100", coerced.UncoercedValue);
            Assert.Equal("100", coerced.Origin!.Provider.Value);
        });
    }

    [Fact]
    public void DirectProperties_DoNotInventAStyledPriorityStack()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new ProbeControl { Counter = 7 };
            var explanation = Explain(control, ProbeControl.CounterProperty, candidates: true);

            Assert.Equal("direct", explanation.Property.RegistrationKind);
            Assert.Equal("7", explanation.EffectiveValue);
            Assert.Null(explanation.Priority);
            Assert.Empty(explanation.Candidates!);
            Assert.Equal("directProperty", explanation.Origin!.Kind);
        });
    }

    [Fact]
    public void AmbiguousPropertyNames_RequireAnExactOwner()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border { Width = 40 };
            control.SetValue(ProvenanceAttachedOwner.WidthProperty, 17d);

            var ambiguous = Assert.IsType<HandlerErrorResult>(ExplainPropertyHandler.Explain(control, "Width"));
            Assert.Equal(DiagnosticErrorCodes.AmbiguousProperty, ambiguous.Error.Code);
            var attached = Explain(control, ProvenanceAttachedOwner.WidthProperty);
            Assert.Equal("attached", attached.Property.RegistrationKind);
            Assert.Equal("17", attached.EffectiveValue);
            Assert.Equal("40", Explain(control, Border.WidthProperty).EffectiveValue);
        });
    }

    [Fact]
    public void LargeStyleSets_AreTruncatedWithoutLosingTheCapturedWinner()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border();
            using var scope = new ControlScope(control, Enumerable.Range(1, 300).Select(index => WidthStyle(index)).ToArray());
            var explanation = Explain(control, Border.WidthProperty, candidates: true);
            var styles = Styles(control);

            Assert.True(explanation.Resolution.Truncated);
            Assert.True(styles.Resolution.Truncated);
            Assert.True(explanation.Candidates!.Count <= 256);
            Assert.True(styles.Styles.Count <= 256);
            Assert.Equal("300", explanation.EffectiveValue);
            Assert.Equal(styles.Styles.Single(style => style.Setters.Any(setter => setter.Wins == true)).SourceId,
                explanation.Origin!.SourceId);
        });
    }

    [Fact]
    public void ObjectValues_AreNotDumpedOrFormattedByApplicationCode()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var control = new Border { Tag = new SecretObject() };
            var explanation = Explain(control, Control.TagProperty);

            Assert.Equal($"<{typeof(SecretObject).FullName}>", explanation.EffectiveValue);
        });
    }

    private static PropertyExplanation Explain(AvaloniaObject target, AvaloniaProperty property, bool candidates = false, int depth = 8) =>
        Assert.IsType<PropertyExplanation>(ExplainPropertyHandler.Explain(target,
            $"{property.OwnerType.FullName}.{property.Name}", candidates, depth));

    private static StylesSnapshot Styles(Visual target, bool includeInactive = false) =>
        Assert.IsType<StylesSnapshot>(StylesHandler.GetStyles(target, includeInactive, null));

    private static Style WidthStyle(double value)
    {
        var style = new Style(selector => selector.OfType<Border>());
        style.Setters.Add(new Setter(Border.WidthProperty, value));
        return style;
    }

    private sealed class ControlScope : IDisposable
    {
        public Window Window { get; }

        public ControlScope(Control control, params IStyle[] styles)
        {
            Window = new Window { Content = control };
            foreach (var style in styles)
                Window.Styles.Add(style);
            Window.Show();
        }

        public void Dispose() => Window.Close();
    }

    private sealed class ProbeControl : Border
    {
        public static readonly StyledProperty<int> InheritedValueProperty =
            AvaloniaProperty.Register<ProbeControl, int>("InheritedValue", 1, inherits: true);
        public static readonly StyledProperty<int> CappedProperty =
            AvaloniaProperty.Register<ProbeControl, int>("Capped", 1, coerce: (_, value) => Math.Min(value, 10));
        public static readonly StyledProperty<int> CoercedDefaultProperty =
            AvaloniaProperty.Register<ProbeControl, int>("CoercedDefault", 20, coerce: (_, value) => Math.Min(value, 5));
        public static readonly DirectProperty<ProbeControl, int> CounterProperty =
            AvaloniaProperty.RegisterDirect<ProbeControl, int>(nameof(Counter), control => control.Counter);

        private int _counter;
        public int Counter
        {
            get => _counter;
            set => SetAndRaise(CounterProperty, ref _counter, value);
        }

        public void SetAlert(bool active) => PseudoClasses.Set(":alert", active);
        public void InheritFrom(AvaloniaObject parent) => InheritanceParent = parent;
    }

    private sealed class ProvenanceAttachedOwner : AvaloniaObject
    {
        public static readonly AttachedProperty<double> WidthProperty =
            AvaloniaProperty.RegisterAttached<ProvenanceAttachedOwner, Control, double>("Width", 0);
    }

    private sealed class CountingSource
    {
        public int ReadCount { get; private set; }
        public double Width { get { ReadCount++; return 42; } }
        public override string ToString() => throw new InvalidOperationException("Do not dump the binding source.");
    }

    private sealed class SecretObject
    {
        public override string ToString() => throw new InvalidOperationException("Do not format application objects.");
    }

    private sealed class ConstantObservable<T>(T value) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            return new Subscription();
        }

        private sealed class Subscription : IDisposable
        {
            public void Dispose() { }
        }
    }

}
