using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FastBindings.StateManagerObjects
{
    /// <summary>
    /// One PropertyChanged handler per source object, shared by every binding that reads it. A change finds the bindings that read
    /// that property by name; before, every binding had its own handler on every object of its path, so a view-model read by a
    /// thousand bindings called a thousand handlers per change, and adding or removing one copied the whole delegate list.
    /// Kept beside the source (a weak table): a hub lives exactly as long as its source.
    /// </summary>
    internal sealed class SourceHub
    {
        private static readonly ConditionalWeakTable<INotifyPropertyChanged, SourceHub> Hubs = new ConditionalWeakTable<INotifyPropertyChanged, SourceHub>();

        private readonly Dictionary<string, List<IHubSubscriber>> _byName = new Dictionary<string, List<IHubSubscriber>>(StringComparer.Ordinal);
        private readonly INotifyPropertyChanged _source;
        private int _count;

        private SourceHub(INotifyPropertyChanged source) => _source = source;

        public static SourceHub Of(INotifyPropertyChanged source) => Hubs.GetValue(source, s => new SourceHub(s));

        public void Add(string name, IHubSubscriber subscriber)
        {
            if (!_byName.TryGetValue(name, out var list))
                _byName[name] = list = new List<IHubSubscriber>(1);
            list.Add(subscriber);
            if (_count++ == 0)
                _source.PropertyChanged += OnPropertyChanged;
        }

        public void Remove(string name, IHubSubscriber subscriber)
        {
            if (!_byName.TryGetValue(name, out var list))
                return;
            var index = list.IndexOf(subscriber);
            if (index < 0)
                return;
            list[index] = list[list.Count - 1];   // order among one property's subscribers does not matter
            list.RemoveAt(list.Count - 1);
            if (list.Count == 0)
                _byName.Remove(name);
            if (--_count == 0)
                _source.PropertyChanged -= OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.PropertyName))
            {
                // every property changed: everyone is told (a copy, since they may unsubscribe while told)
                var all = new List<IHubSubscriber>(_count);
                foreach (var list in _byName.Values)
                    all.AddRange(list);
                foreach (var subscriber in all)
                    subscriber.OnSourceChanged(sender, args);
                return;
            }
            if (!_byName.TryGetValue(args.PropertyName!, out var bound))
                return;
            if (bound.Count == 1)
            {
                bound[0].OnSourceChanged(sender, args);
                return;
            }
            foreach (var subscriber in bound.ToArray())
                subscriber.OnSourceChanged(sender, args);
        }
    }

    internal interface IHubSubscriber
    {
        void OnSourceChanged(object? sender, PropertyChangedEventArgs args);
    }
}
