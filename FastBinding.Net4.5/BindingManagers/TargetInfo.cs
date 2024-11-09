using FastBindings.Helpers;
using System;
using System.Windows;

namespace FastBindings.BindingManagers
{
    internal class TargetInfo
    {
        public bool IsIniailized { get; private set; } = false;
        public bool IsDependencyObject { get; private set; } = false;
        private string _observerTarget = null;

        public string ObserverTarget
        {
            get
            {
                if (_observerTarget == null)
                {
                    bool hasObserver = !string.IsNullOrEmpty(Target) && Target.Length > 2 && Target[0] == '-'
                     && Target[Target.Length - 1] == '-';
                    _observerTarget = hasObserver ?  Target.Substring(1, Target.Length - 2) : string.Empty;
                }
                return _observerTarget;
            }
        }
        public string Target { get; set; } = string.Empty;

        public bool IsObserver => !string.IsNullOrEmpty(ObserverTarget);

        public void Initialize(DependencyObject obj)
        {
            IsIniailized = true;
            if (IsObserver)
                return;

            var targetParser = new PropertyPathParser(Target);
            if (targetParser.IsValid)
            {
                IsDependencyObject = true;
                DependencyProperty depend = null;
                var child = targetParser.CalculateSource(obj);
                Initialize(child, null);
                if (child != null)
                {
                    depend = ReflectionUtility.FindDependencyPropertyByName(child, targetParser.Property);
                    Initialize(child, depend);
                }
            }
        }

        private void Initialize(DependencyObject obj, DependencyProperty property)
        {
            _targetObjectRef.Target = obj;
            _targetPropertyRef.Target = property;
        }

        public DependencyObject TargetObj => _targetObjectRef.Target as DependencyObject;
        public DependencyProperty TargetObjProperty => _targetPropertyRef.Target as DependencyProperty;

        private readonly WeakReference _targetObjectRef = new WeakReference(null);
        private readonly WeakReference _targetPropertyRef = new WeakReference(null);
        public SubscriberProxy Proxy { get; set; }
    }

}
