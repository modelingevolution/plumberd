using Microsoft.AspNetCore.Components;

namespace Checkers.Common.Controls
{
    public interface IControl
    {
        
    }

    public interface IControlController<TControl> where TControl:ComponentBase
    {
        
    }
}