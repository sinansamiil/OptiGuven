using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using OptiGuven.Web.Tools;

namespace OptiGuven.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // --- SİMÜLATÖRÜ BURAYA BAĞLIYORUZ ---
            System.Threading.Thread simulator = new System.Threading.Thread(OptiGuven.Web.Tools.SensorSimulasyon.Baslat);
            simulator.IsBackground = true;
            simulator.Start();
        }
    }
}
