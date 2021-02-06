using System.Threading.Tasks;

namespace ModelingEvolution.Plumberd.Binding
{
    public static class HandlerBinderExtensions
    {
        public static IEventHandlerBinder Discover(this IEventHandlerBinder binder, bool searchInProperties, BindingFlags flags)
        {
            BinderFilter f = new BinderFilter(flags);
            return binder.Discover(searchInProperties, f.Filter);
        }
        
    }
}