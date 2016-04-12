using System.Web.Mvc;
using System.Web.Routing;
using Nop.Web.Framework.Mvc.Routes;

namespace Nop.Plugin.Payments.Paylike
{
    public partial class RouteProvider : IRouteProvider
    {
        public void RegisterRoutes(RouteCollection routes)
        {
            routes.MapRoute("Plugin.Payments.Paylike.FinishOrder",
                 "Plugins/PaymentPaylike/FinishOrder",
                 new { controller = "Paylike", action = "FinishOrder" },
                 new[] { "Nop.Plugin.Payments.Paylike.Controllers" }
            );
        }

        public int Priority
        {
            get
            {
                return 0;
            }
        }
    }
}
