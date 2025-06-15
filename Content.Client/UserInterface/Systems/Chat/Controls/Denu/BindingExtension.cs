using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

public sealed class Disposable : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    public Disposable(Action dispose)
    {
        _dispose = dispose ?? throw new ArgumentNullException(nameof(dispose));
    }

    public void Dispose()
    {
        if (!_disposed) {
            _disposed = true;
            _dispose();
        }
    }
}

public sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables;
    private bool _disposed;

    public CompositeDisposable(params IDisposable[] disposables)
    {
        _disposables = new List<IDisposable>(disposables);
    }

    public void Dispose()
    {
        if (!_disposed) {
            _disposed = true;
            foreach (var disposable in _disposables) {
                disposable?.Dispose();
            }

            _disposables.Clear();
        }
    }
}

public sealed class Observable<T> : INotifyPropertyChanged
{
    private T _value;

    public T Value {
        get => _value;
        set {
            if (!EqualityComparer<T>.Default.Equals(_value, value)) {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Observable(T initialValue = default!)
    {
        _value = initialValue;
    }

    public static implicit operator T(Observable<T> observable)
    {
        ArgumentNullException.ThrowIfNull(observable);
        return observable.Value;
    }
}

public static class ReactiveBinding
{
    public static IDisposable Bind<T>(
        Observable<T> source,
        Control target,
        string propertyName,
        Func<T, object?>? converter = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(propertyName);

        var property = target.GetType().GetProperty(propertyName);
        if (property == null) {
            throw new ArgumentException($"Property '{propertyName}' not found on type {target.GetType().Name}");
        }

        void UpdateTarget()
        {
            try {
                var value = converter?.Invoke(source.Value) ?? source.Value;
                property.SetValue(target, value);
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed to update binding: {ex.Message}");
            }
        }

        UpdateTarget();

        void Handler(object? sender, PropertyChangedEventArgs e) => UpdateTarget();
        source.PropertyChanged += Handler;

        return new Disposable(() => source.PropertyChanged -= Handler);
    }

    public static IDisposable BindTwoWay<T>(
        Observable<T> source,
        Control target,
        string propertyName,
        string? eventName = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(propertyName);

        var oneWay = Bind(source, target, propertyName);

        var disposables = new List<IDisposable> { oneWay };

        switch (target) {
            case LineEdit lineEdit when propertyName == "Text":
                lineEdit.OnTextChanged += args => {
                    if (args.Text is T typedValue)
                        source.Value = typedValue;
                };
                break;

            case CheckBox checkBox when propertyName == "Pressed":
                checkBox.OnPressed += args => {
                    if (checkBox.Pressed is T typedValue)
                        source.Value = typedValue;
                };
                break;

            case SpinBox spinBox when propertyName == "Value":
                spinBox.ValueChanged += args => {
                    if (args.Value is T typedValue)
                        source.Value = typedValue;
                };
                break;

            default:
                TryBindGenericEvent(source, target, propertyName, eventName, disposables);
                break;
        }

        return new CompositeDisposable(disposables.ToArray());
    }

    private static void TryBindGenericEvent<T>(
        Observable<T> source,
        Control target,
        string propertyName,
        string? eventName,
        List<IDisposable> disposables)
    {
        eventName ??= $"On{propertyName}Changed";
        var eventInfo = target.GetType().GetEvent(eventName);

        if (eventInfo == null) {
            eventInfo = target.GetType().GetEvent($"{propertyName}Changed") ??
                        target.GetType().GetEvent($"On{propertyName}");
        }

        if (eventInfo != null) {
            var property = target.GetType().GetProperty(propertyName);
            if (property != null) {
                var handler = new PropertyChangedHandler<T>(source, target, property);

                var addMethod = eventInfo.GetAddMethod();
                if (addMethod != null) {
                    try {
                        var delegateType = eventInfo.EventHandlerType!;
                        var invokeMethod = delegateType.GetMethod("Invoke")!;
                        var parameters = invokeMethod.GetParameters();

                        Delegate? del = parameters.Length switch {
                            0 => (Action)(() => handler.UpdateSource()),
                            1 => (Action<object>)(_ => handler.UpdateSource()),
                            2 => (EventHandler)((s, e) => handler.UpdateSource()),
                            _ => null
                        };

                        if (del != null && delegateType.IsAssignableFrom(del.GetType())) {
                            addMethod.Invoke(target, new[] { del });
                            var removeMethod = eventInfo.GetRemoveMethod();
                            disposables.Add(new Disposable(() =>
                                removeMethod?.Invoke(target, new[] { del })));
                        }
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"Failed to bind to event {eventName}: {ex.Message}");
                    }
                }
            }
        }
    }

    private class PropertyChangedHandler<T>
    {
        private readonly Observable<T> _source;
        private readonly Control _target;
        private readonly PropertyInfo _property;

        public PropertyChangedHandler(Observable<T> source, Control target, PropertyInfo property)
        {
            _source = source;
            _target = target;
            _property = property;
        }

        public void UpdateSource()
        {
            try {
                var value = _property.GetValue(_target);
                if (value is T typedValue) {
                    _source.Value = typedValue;
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Failed to update source: {ex.Message}");
            }
        }
    }
}


// public static class ReactiveBinding
// {
//     public static IDisposable Bind<T>(
//         Observable<T> source,
//         Control target,
//         string propertyName,
//         Func<T, object?>? converter = null)
//     {
//         ArgumentNullException.ThrowIfNull(source);
//         ArgumentNullException.ThrowIfNull(target);
//         ArgumentNullException.ThrowIfNullOrWhiteSpace(propertyName);
//
//         var property = target.GetType().GetProperty(propertyName);
//         if (property == null) {
//             throw new ArgumentException($"Property '{propertyName}' not found on type {target.GetType().Name}");
//         }
//
//         void UpdateTarget()
//         {
//             try {
//                 var value = converter?.Invoke(source.Value) ?? source.Value;
//                 property.SetValue(target, value);
//             }
//             catch (Exception ex) {
//                 Console.WriteLine($"Failed to update binding: {ex.Message}");
//             }
//         }
//
//         UpdateTarget();
//
//         void Handler(object? sender, PropertyChangedEventArgs e) => UpdateTarget();
//         source.PropertyChanged += Handler;
//
//         return new Disposable(() => source.PropertyChanged -= Handler);
//     }
//
//     public static IDisposable BindTwoWay<T>(
//         Observable<T> source,
//         Control target,
//         string propertyName,
//         string? eventName = null)
//     {
//         ArgumentNullException.ThrowIfNull(source);
//         ArgumentNullException.ThrowIfNull(target);
//         ArgumentNullException.ThrowIfNullOrWhiteSpace(propertyName);
//
//         var oneWay = Bind(source, target, propertyName);
//
//         eventName ??= $"{propertyName}Changed";
//         var eventInfo = target.GetType().GetEvent(eventName);
//
//         if (eventInfo != null) {
//             var property = target.GetType().GetProperty(propertyName);
//             if (property == null) {
//                 return oneWay;
//             }
//
//             EventHandler handler = (sender, e) => {
//                 try {
//                     var value = property.GetValue(target);
//                     if (value is T typedValue) {
//                         source.Value = typedValue;
//                     }
//                 }
//                 catch (Exception ex) {
//                     Console.WriteLine($"Failed to update source from target: {ex.Message}");
//                 }
//             };
//
//             if (eventInfo.EventHandlerType == typeof(EventHandler)) {
//                 eventInfo.AddEventHandler(target, handler);
//                 return new CompositeDisposable(
//                     oneWay,
//                     new Disposable(() => eventInfo.RemoveEventHandler(target, handler))
//                 );
//             }
//             else if (eventInfo.EventHandlerType == typeof(Action)) {
//                 Action action = () => handler(target, EventArgs.Empty);
//                 eventInfo.AddEventHandler(target, action);
//                 return new CompositeDisposable(
//                     oneWay,
//                     new Disposable(() => eventInfo.RemoveEventHandler(target, action))
//                 );
//             }
//             else if (eventInfo.EventHandlerType?.IsGenericType == true &&
//                      eventInfo.EventHandlerType.GetGenericTypeDefinition() == typeof(Action<>)) {
//                 var paramType = eventInfo.EventHandlerType.GetGenericArguments()[0];
//                 var handlerMethod = handler.Method;
//                 var delegateHandler = Delegate.CreateDelegate(
//                     eventInfo.EventHandlerType,
//                     handler.Target,
//                     handlerMethod);
//
//                 eventInfo.AddEventHandler(target, delegateHandler);
//                 return new CompositeDisposable(
//                     oneWay,
//                     new Disposable(() => eventInfo.RemoveEventHandler(target, delegateHandler))
//                 );
//             }
//         }
//
//         return oneWay;
//     }
// }

public sealed class BindingContext : IDisposable
{
    private readonly List<IDisposable> _bindings = new();
    private bool _disposed;

    public void Bind<T>(Observable<T> source, Control target, string property)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        _bindings.Add(ReactiveBinding.Bind(source, target, property));
    }

    public void BindTwoWay<T>(Observable<T> source, Control target, string property, string? eventName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        _bindings.Add(ReactiveBinding.BindTwoWay(source, target, property, eventName));
    }

    public void Clear()
    {
        foreach (var binding in _bindings) {
            binding?.Dispose();
        }

        _bindings.Clear();
    }

    public void Dispose()
    {
        if (!_disposed) {
            _disposed = true;
            Clear();
        }
    }
}

public static class ControlBindingExtensions
{
    private static readonly ConditionalWeakTable<Control, BindingContext> _contexts = new();
        
    public static BindingContext GetBindingContext(this Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _contexts.GetOrCreateValue(control);
    }
        
    public static void BindToLabel(this Control control, string labelName, Observable<string> observable)
    {
        if (control.FindControl<Label>(labelName) is { } label)
        {
            control.GetBindingContext().Bind(observable, label, "Text");
        }
    }
}
