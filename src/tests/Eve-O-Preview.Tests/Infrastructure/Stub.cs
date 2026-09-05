using System;
using System.Reflection;
using System.Threading.Tasks;

namespace EveOPreview.Tests.Infrastructure;

public class Stub : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler;
    public static T Create<T>(Func<MethodInfo, object[], object> handler = null) where T : class
    {
        T proxy = DispatchProxy.Create<T, Stub>();
        ((Stub)(object)proxy).Handler = handler;
        return proxy;
    }
    protected override object Invoke(MethodInfo method, object[] args) => Handler == null ? Default(method.ReturnType) : Handler(method, args);
    public static object Default(Type type)
    {
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type == typeof(Task<bool>)) return Task.FromResult(true);
        return type.IsValueType && type != typeof(void) ? Activator.CreateInstance(type) : null;
    }
}
