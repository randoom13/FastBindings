using FastBindings.Helpers;

namespace FastBindings.BindingManagers
{
    internal class TargetInfo
    {
        private string? _observerTarget = null;
        private readonly WeakReference _targetObjectRef = new WeakReference(null);
        private readonly WeakReference _targetPropertyRef = new WeakReference(null);

        public string ObserverTarget
        {
            get
            {
                if (_observerTarget == null)
                {
                    bool hasObserver = !string.IsNullOrEmpty(Target) && Target.Length > 2 && Target[0] == '-'
                     && Target[Target.Length - 1] == '-';
                    _observerTarget = hasObserver ? Target.Substring(1, Target.Length - 2) : string.Empty;
                }
                return _observerTarget;
            }
        }
        public string Target { get; set; } = string.Empty;

        public bool IsObserver => !string.IsNullOrEmpty(ObserverTarget);

        public void Initialize(BindableObject obj)
        {
            IsIniailized = true;
            if (IsObserver)
                return;

            var targetParser = new PropertyPathParser(Target);
            if (targetParser.IsValid)
            {
                IsDependencyObject = true;
                var child = targetParser.CalculateSource(obj);
                Initialize(child, null);
                if (child != null)
                {
                    var depend = ReflectionUtility.FindDependencyPropertyByName(child, targetParser.Property);
                    Initialize(child, depend);
                }
            }
        }

        private void Initialize(BindableObject? obj, BindableProperty? property)
        {
            _targetObjectRef.Target = obj;
            _targetPropertyRef.Target = property;
        }

        public BindableObject? TargetObj => _targetObjectRef.Target as BindableObject;
        public BindableProperty? TargetObjProperty => _targetPropertyRef.Target as BindableProperty;
        public SubscriberProxy? Proxy { get; set; }
        public bool IsIniailized { get; private set; } = false;
        public bool IsDependencyObject { get; private set; } = false;
    }

}
