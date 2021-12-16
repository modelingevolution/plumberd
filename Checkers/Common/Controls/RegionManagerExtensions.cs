using Checkers.Common.RegionManagement;

namespace Checkers.Common.Controls
{
    public static class RegionManagerExtensions
    {
        public static void SetContent(this IContentControl control, Type controlType, long id, ControlParameter[] args)
        {
            control.Control = new RenderControl(id, builder =>
            {
                builder.OpenComponent(0, controlType);
                for (int i = 0; i < args.Length; i++)
                {
                    builder.AddAttribute(1+i, args[i].Name, args[i].Value);
                }
                builder.CloseComponent();
            });
        }
        public static void SetContent(this IContentControl control, Type controlType, long id, object dataContext)
        {
            control.Control = new RenderControl(id, builder =>
            {
                builder.OpenComponent(0, controlType);
                builder.AddAttribute(1, "DataContext", dataContext);
                builder.CloseComponent();
            });
        }
        public static void AddContent(this IItemsControl control, Type controlType, long id, int seq, ControlParameter[] args)
        {
            control.Children.Add(new RenderControl(id, builder =>
            {
                builder.OpenComponent(seq, controlType);
                for (int i = 0; i < args.Length; i++)
                {
                    builder.AddAttribute(seq + i, args[i].Name, args[i].Value);
                }
                
                builder.CloseComponent();
            }));
        }
        public static void AddContent(this IItemsControl control, Type controlType, long id, object dataContext)
        {
            
            int index = control.Children.Count;
            control.Children.Add( new RenderControl(id, builder =>
            {
                builder.OpenComponent(2*index, controlType);
                builder.AddAttribute(2*index+1, "DataContext", dataContext);
                builder.CloseComponent();
            }));
        }
    }
}
