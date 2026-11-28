using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FastBindings.StateManagerObjects;
using FastBindings.Tests.Views;
using Xunit;

namespace FastBindings.Tests
{
    /// <summary>The view-model a control's events are passed to: what each method was given, in order.</summary>
    public sealed class EventsModel : Observable
    {
        public List<object?> Notes { get; } = new List<object?>();
        public int Count { get; private set; }
        public EventsModel Child => _child ??= new EventsModel();
        private EventsModel? _child;
        public IReadOnlyList<EventsRow> Rows { get; } = new[] { new EventsRow(), new EventsRow() };

        public void Noted(object? args) => Notes.Add(args);

        /// <summary>No parameter: the event is only a signal.</summary>
        public void Counted() => Count++;
    }

    public sealed class EventsRow : Observable
    {
        private bool _ticked;
        public bool Ticked { get => _ticked; set => Set(ref _ticked, value); }
    }

    /// <summary>
    /// An event source passes a control's event to a method of the view-model (<c>Sources="$[[this].Poked]" Target="Noted("</c>):
    /// once per event, never before one — not when the binding starts, nor when the DataContext changes — and to the DataContext
    /// the control has when the event fires.
    /// </summary>
    public class EventSourceTests
    {
        [AvaloniaFact]
        public void The_method_is_called_once_per_event_and_never_before_one()
        {
            var vm = new EventsModel();
            using var ui = Ui.Show(new Events(), vm);
            Assert.Empty(vm.Notes);   // started: no event yet

            ui.Find<Poker>("Poker").Poke();
            ui.Find<Poker>("Poker").Poke();
            Ui.Settle();
            Assert.Equal(2, vm.Notes.Count);
            var args = Assert.IsType<EventInfoArgs>(vm.Notes[0]);
            Assert.Equal("Poked", args.EventName);
            Assert.IsType<PokeArgs>(args.EventArgs);
        }

        [AvaloniaFact]
        public void A_DataContext_change_calls_nothing_and_later_events_reach_the_new_view_model()
        {
            var first = new EventsModel();
            using var ui = Ui.Show(new Events(), first);
            var second = new EventsModel();
            ui.View.DataContext = second;
            Ui.Settle();
            Assert.Empty(first.Notes);
            Assert.Empty(second.Notes);

            ui.Find<Poker>("Poker").Poke();
            Ui.Settle();
            Assert.Empty(first.Notes);
            Assert.Single(second.Notes);
        }

        [AvaloniaFact]
        public void A_routed_event_reaches_a_method_of_a_child_view_model()
        {
            var vm = new EventsModel();
            using var ui = Ui.Show(new Events(), vm);
            ui.Find<Button>("Clicked").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Ui.Settle();
            Assert.Empty(vm.Notes);
            Assert.IsType<RoutedEventArgs>(Assert.IsType<EventInfoArgs>(Assert.Single(vm.Child.Notes)).EventArgs);
        }

        [AvaloniaFact]
        public void A_method_without_a_parameter_is_called_without_the_event()
        {
            var vm = new EventsModel();
            using var ui = Ui.Show(new Events(), vm);
            Assert.Equal(0, vm.Count);
            ui.Find<Poker>("NoParameter").Poke();
            Ui.Settle();
            Assert.Equal(1, vm.Count);
        }

        [AvaloniaFact]
        public void A_check_box_in_a_template_tells_the_panels_view_model_through_DataContextSource()
        {
            var vm = new EventsModel();
            using var ui = Ui.Show(new Events(), vm);
            var boxes = ui.Find<ItemsControl>("Rows").GetVisualDescendants().OfType<CheckBox>().ToList();
            Assert.Equal(2, boxes.Count);
            Assert.Equal(0, vm.Count);

            boxes[1].IsChecked = true;
            Ui.Settle();
            Assert.True(vm.Rows[1].Ticked);   // the row's own two-way binding still writes back
            Assert.Equal(1, vm.Count);
            boxes[0].IsChecked = true;
            boxes[1].IsChecked = false;
            Ui.Settle();
            Assert.Equal(3, vm.Count);
        }
    }
}
