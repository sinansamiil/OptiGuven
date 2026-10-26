using System.Web;
using System.Web.Mvc;

namespace OptiGuven.Web.Controllers
{
    public class YetkiKontrolAttribute : ActionFilterAttribute
    {
        public string HedefRol { get; set; }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var session = filterContext.HttpContext.Session;

            // 1. Kullanıcı giriş yapmış mı?
            if (session["Rol"] == null)
            {
                // Giriş yapmamışsa, onu Login sayfasına atıyoruz
                filterContext.Result = new RedirectResult("~/Account/Login");
                return;
            }

            // 2. Eğer belirli bir rol (örn: Admin) gerekiyorsa ve kullanıcıda yoksa
            if (!string.IsNullOrEmpty(HedefRol) && session["Rol"].ToString() != HedefRol)
            {
                // Yetkisi yoksa ana sayfaya (veya hata sayfasına) atıyoruz
                filterContext.Result = new RedirectResult("~/Account/Login?hata=yetkisiz");
            }

            base.OnActionExecuting(filterContext);
        }
    }
}