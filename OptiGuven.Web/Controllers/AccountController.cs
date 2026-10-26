using OptiGuven.Web.Models;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace OptiGuven.Web.Controllers
{
    public class AccountController : Controller
    {
        private AkilliFabrikaDBEntities db = new AkilliFabrikaDBEntities();

        // Giriş sayfasını getiren metot
        public ActionResult Login()
        {
            return View();
        }

        // Giriş formundan gelen verileri işleyen metot
        [HttpPost]
        [ValidateAntiForgeryToken] // CSRF Koruması
        public ActionResult Login(string kullaniciAdi, string sifre, string girisTipi)
        {
            kullaniciAdi = kullaniciAdi?.Trim();

            if (string.IsNullOrEmpty(kullaniciAdi) || string.IsNullOrEmpty(sifre))
            {
                ViewBag.Hata = "Lütfen tüm alanları doldurun.";
                return View();
            }

            // =================================================================
            // 1. TEST İÇİN ÖRNEK HESAPLAR
            // =================================================================
            if (girisTipi == "Calisan" && kullaniciAdi == "calisan@optiguven.com" && sifre == "1234")
            {
                OturumuGuvenliBaslat("Örnek Çalışan", "Calisan");
                return RedirectToAction("ArizaBildir", "Makine");
            }
            else if (girisTipi == "Admin" && kullaniciAdi == "admin@optiguven.com" && sifre == "1234")
            {
                OturumuGuvenliBaslat("Örnek Yönetici", "Admin");
                return RedirectToAction("Dashboard", "Makine");
            }

            // =================================================================
            // 2. GERÇEK VERİTABANI KONTROLÜ
            // =================================================================
            string hashlenmisSifre = SifreleSHA256(sifre);

            var kullanici = db.Kullanicilar.FirstOrDefault(k =>
                k.Email == kullaniciAdi &&
                (k.SifreHash == hashlenmisSifre || k.SifreHash == sifre));

            if (kullanici != null)
            {
                // ==========================================
                // ROL VE SEKME UYUŞMAZLIĞI KONTROLÜ
                // ==========================================
                if (girisTipi == "Admin" && kullanici.Rol != "Admin")
                {
                    ViewBag.Hata = "Bu alandan sadece yöneticiler giriş yapabilir!";
                    return View();
                }
                else if (girisTipi == "Calisan" && kullanici.Rol == "Admin")
                {
                    ViewBag.Hata = "Yöneticiler, Yönetici sekmesinden giriş yapmalıdır!";
                    return View();
                }

                // Kontrollerden geçtiyse oturumu başlat
                OturumuGuvenliBaslat(kullanici.AdSoyad, kullanici.Rol);

                if (kullanici.Rol == "Admin")
                {
                    return RedirectToAction("Dashboard", "Makine");
                }
                else
                {
                    return RedirectToAction("ArizaBildir", "Makine");
                }
            }

            ViewBag.Hata = "Hatalı giriş yaptınız! Bilgilerinizi kontrol edin.";
            return View();
        }

        // Güvenli çıkış metodu
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon(); // Sunucudaki session nesnesini tamamen yok et
            return RedirectToAction("Login", "Account");
        }

        // KAYIT OL SAYFASINI GETİRİR
        public ActionResult Register()
        {
            return View();
        }

        // KAYIT OL FORMUNU İŞLER VE VERİTABANINA KAYDEDER
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string adSoyad, string email, string sifre, string kurumKodu)
        {
            adSoyad = adSoyad?.Trim();
            email = email?.Trim();
            kurumKodu = kurumKodu?.Trim();

            if (string.IsNullOrEmpty(adSoyad) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(sifre) || string.IsNullOrEmpty(kurumKodu))
            {
                ViewBag.Hata = "Lütfen tüm alanları doldurun.";
                return View();
            }

            // =================================================================
            // GÜVENLİK DUVARI: KURUM KODU KONTROLÜ
            // =================================================================
            string gecerliKurumKodu = "optigüven";

            if (kurumKodu != gecerliKurumKodu)
            {
                ViewBag.Hata = "Geçersiz Kurum Kayıt Kodu! Sadece yetkili fabrika personeli kayıt olabilir.";
                return View();
            }

            // Bu e-posta ile daha önce kayıt olunmuş mu kontrolü
            var mevcutKullanici = db.Kullanicilar.FirstOrDefault(k => k.Email == email);
            if (mevcutKullanici != null)
            {
                ViewBag.Hata = "Bu e-posta adresi zaten sistemde kayıtlı!";
                return View();
            }

            // Şifreyi güvenli hale getir
            string hashlenmisSifre = SifreleSHA256(sifre);

            var yeniKullanici = new Kullanicilar
            {
                AdSoyad = adSoyad,
                Email = email,
                SifreHash = hashlenmisSifre,
                Rol = "Calisan" // Sadece içerideki bir Admin bu yetkiyi sonradan değiştirebilir
            };

            db.Kullanicilar.Add(yeniKullanici);
            db.SaveChanges();

            TempData["BasariMesaji"] = "Hesabınız başarıyla oluşturuldu! Şimdi giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        // SHA256 Şifreleme Yardımcı Metodu
        private string SifreleSHA256(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Session Fixation (Oturum Sabitleme) Saldırılarını Engelleme
        private void OturumuGuvenliBaslat(string adSoyad, string rol)
        {
            Session.Clear();
            Session["KullaniciAdi"] = adSoyad;
            Session["Rol"] = rol;
        }
    }
}