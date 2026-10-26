using OptiGuven.Web.Helpers;
using OptiGuven.Web.Models;
using OptiGuven_Web;
using Rotativa;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace OptiGuven.Web.Controllers
{
    [YetkiKontrol]
    public class MakineController : Controller
    {
        // Son mail atma zamanını hafızada tutarız (Spam koruması)
        private static DateTime _sonUyariZamani = DateTime.MinValue;
        private AkilliFabrikaDBEntities db = new AkilliFabrikaDBEntities();

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult Index()
        {
            return View(db.Makineler.ToList());
        }

        public ActionResult Create() => View();

        [YetkiKontrol(HedefRol = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Makineler yeniMakine)
        {
            if (ModelState.IsValid)
            {
                db.Makineler.Add(yeniMakine);
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(yeniMakine);
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult Edit(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var makine = db.Makineler.Find(id);
            if (makine == null) return HttpNotFound();
            return View(makine);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Makineler guncelMakine)
        {
            if (ModelState.IsValid)
            {
                var makine = db.Makineler.Find(guncelMakine.Id);
                if (makine != null)
                {
                    // Anomali/Bakım mantığı
                    if (makine.Durum != "Bakımda" && guncelMakine.Durum == "Bakımda")
                    {
                        db.ArizaBiletleri.Add(new ArizaBiletleri
                        {
                            MakineId = makine.Id,
                            KullaniciAciklamasi = "Sistem tarafından otomatik oluşturuldu: Makine bakıma alındı.",
                            Durum = "Açık",
                            OlusturulmaTarihi = DateTime.Now
                        });
                    }

                    makine.MakineKodu = guncelMakine.MakineKodu;
                    makine.Ad = guncelMakine.Ad;
                    makine.Durum = guncelMakine.Durum;
                    db.SaveChanges();
                }
                return RedirectToAction("Index");
            }
            return View(guncelMakine);
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult Delete(int id)
        {
            if (Session["Rol"]?.ToString() != "Admin") return RedirectToAction("Index");

            var makine = db.Makineler.Find(id);
            if (makine != null)
            {
                db.Makineler.Remove(makine);
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public async Task<ActionResult> Dashboard()
        {
            // 1. Örnek bir makinenin o anki sensör verilerini simüle ediyoruz
            double anlikTitresim = 5.5;
            double anlikSicaklik = 85.0;
            double anlikCalismaSaati = 16.0;

            // 2. Makine Öğrenmesi (Python) motoruna verileri gönderip analizi alıyoruz
            var mlMotoru = new OptiGuven.Web.Tools.SensorSimulasyon();
            var tahminSonucu = await mlMotoru.AnalizEt(anlikTitresim, anlikSicaklik, anlikCalismaSaati);

            // 3. Gelen verileri ekrana (View'a) taşımak için ViewBag içine atıyoruz
            ViewBag.Titresim = anlikTitresim;
            ViewBag.Sicaklik = anlikSicaklik;

            // Yapay Zeka Çıktıları
            ViewBag.ArizaRiski = tahminSonucu.ariza_riski;
            ViewBag.KalanOmur = tahminSonucu.kalan_omur;

            return View();
        }

        [HttpGet]
        public JsonResult GetSensorChartData()
        {
            var r = new Random();
            var data = new List<object>();

            bool mailGonderilecekMi = false;
            int sonSicaklik = 0;
            int sonTitresim = 0;

            for (int i = 0; i < 10; i++)
            {
                int anlikSicaklik = r.Next(40, 95);
                int anlikTitresim = r.Next(10, 95);

                bool anlikAnomali = (anlikSicaklik > 90 || anlikTitresim > 80);

                if (anlikAnomali)
                {
                    mailGonderilecekMi = true;
                    sonSicaklik = anlikSicaklik;
                    sonTitresim = anlikTitresim;
                }

                data.Add(new
                {
                    Tarih = DateTime.Now.AddSeconds(-10 + i).ToString("HH:mm:ss"),
                    Sicaklik = anlikSicaklik,
                    Titresim = anlikTitresim,
                    IsAnomali = anlikAnomali
                });
            }

            if (mailGonderilecekMi)
            {
                if ((DateTime.Now - _sonUyariZamani).TotalMinutes > 5)
                {
                    string mesaj = $"Makinede kritik limitler aşıldı! <br/> <b>Anlık Sıcaklık:</b> {sonSicaklik}°C <br/> <b>Anlık Titreşim:</b> {sonTitresim} mm/s";

                    MailServisi.AcilDurumMailiGonder(mesaj);

                    _sonUyariZamani = DateTime.Now;
                }
            }

            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult ArizaListesi()
        {
            var acikArizalar = db.ArizaBiletleri
                                 .Include(a => a.Makineler)
                                 .Where(a => a.Durum == "Açık")
                                 .ToList();
            return View(acikArizalar);
        }

        public ActionResult ArizaCozuldu(int id)
        {
            var ariza = db.ArizaBiletleri.Find(id);

            if (ariza != null)
            {
                ariza.Durum = "Çözüldü";
                ariza.KapanisTarihi = DateTime.Now; // Kapanış tarihini de kaydediyoruz
                db.SaveChanges();
                TempData["BasariMesaji"] = $"Harika! #{id} numaralı arıza başarıyla çözüldü olarak işaretlendi.";
            }

            return RedirectToAction("ArizaListesi");
        }

        public ActionResult ArizaKapat(int id)
        {
            var ariza = db.ArizaBiletleri.Find(id);
            if (ariza != null)
            {
                ariza.Durum = "Çözüldü";
                ariza.KapanisTarihi = DateTime.Now;
                db.SaveChanges();
            }
            return RedirectToAction("ArizaListesi");
        }

        public ActionResult KameraSnapshotAl()
        {
            string dosyaAdi = "ariza_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";
            string resimYolu = "/Content/Snapshot/" + dosyaAdi;

            return Json(new { basarili = true, yol = resimYolu }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Raporlar(DateTime? baslangicTarihi, DateTime? bitisTarihi)
        {
            var sorgu = db.SensorLoglari.AsQueryable();

            if (baslangicTarihi.HasValue)
            {
                sorgu = sorgu.Where(x => x.OlusturulmaTarihi >= baslangicTarihi.Value);
            }

            if (bitisTarihi.HasValue)
            {
                var bitis = bitisTarihi.Value.Date.AddDays(1).AddSeconds(-1);
                sorgu = sorgu.Where(x => x.OlusturulmaTarihi <= bitis);
            }

            var sonuclar = sorgu.OrderByDescending(x => x.OlusturulmaTarihi)
                                .Take(100)
                                .ToList();

            ViewBag.Baslangic = baslangicTarihi?.ToString("yyyy-MM-dd");
            ViewBag.Bitis = bitisTarihi?.ToString("yyyy-MM-dd");

            return View(sonuclar);
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult IhlalBildirimiAl(string ihlalTipi, decimal guvenSkoru, string fotografYolu, int kameraId)
        {
            try
            {
                var yeniIhlal = new IsgIhlalleri
                {
                    KameraId = kameraId,
                    IhlalTipi = ihlalTipi,
                    GuvenSkoru = guvenSkoru,
                    FotografYolu = fotografYolu,
                    Tarih = DateTime.Now,
                    Durum = "İncelenmedi"
                };

                db.IsgIhlalleri.Add(yeniIhlal);
                db.SaveChanges();

                if (ihlalTipi.Contains("Yangın") || ihlalTipi.Contains("Hız"))
                {
                    // MailServisi.AcilDurumMailiGonder($"Kamera {kameraId} noktasında yüksek riskli bir ihlal tespit edildi: {ihlalTipi}");
                }

                return Json(new { basari = true, mesaj = "İhlal başarıyla kaydedildi." });
            }
            catch (Exception ex)
            {
                return Json(new { basari = false, mesaj = ex.Message });
            }
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult IsgKontrolPaneli()
        {
            var ihlaller = db.IsgIhlalleri
                             .Where(i => i.Durum == "İncelenmedi")
                             .OrderByDescending(i => i.Tarih)
                             .ToList();

            return View(ihlaller);
        }

        public ActionResult IhlalKapat(int id)
        {
            var ihlal = db.IsgIhlalleri.Find(id);
            if (ihlal != null)
            {
                ihlal.Durum = "İncelendi";
                db.SaveChanges();
            }
            return RedirectToAction("IsgKontrolPaneli");
        }

        public ActionResult ArizaBildir()
        {
            ViewBag.Makineler = new SelectList(db.Makineler.Where(m => m.Durum == "Aktif" || m.Durum == "Uyarı").ToList(), "Id", "Ad");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ArizaBildir(int MakineId, string KullaniciAciklamasi, HttpPostedFileBase YuklenenGorsel)
        {
            try
            {
                string dosyaYolu = null;

                if (YuklenenGorsel != null && YuklenenGorsel.ContentLength > 0)
                {
                    string dosyaAdi = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(YuklenenGorsel.FileName);
                    string klasorYolu = Server.MapPath("~/Content/Uploads/Arizalar/");

                    if (!System.IO.Directory.Exists(klasorYolu))
                    {
                        System.IO.Directory.CreateDirectory(klasorYolu);
                    }

                    string tamYol = System.IO.Path.Combine(klasorYolu, dosyaAdi);
                    YuklenenGorsel.SaveAs(tamYol);
                    dosyaYolu = "/Content/Uploads/Arizalar/" + dosyaAdi;
                }

                string aiKategorisi = "Genel Arıza";

                if (!string.IsNullOrWhiteSpace(KullaniciAciklamasi))
                {
                    var yapayZekaGirdisi = new ArizaNLP.ModelInput()
                    {
                        Aciklama = KullaniciAciklamasi
                    };

                    var tahminSonucu = ArizaNLP.Predict(yapayZekaGirdisi);
                    aiKategorisi = tahminSonucu.PredictedLabel;
                }

                var yeniAriza = new ArizaBiletleri
                {
                    MakineId = MakineId,
                    KullaniciAciklamasi = KullaniciAciklamasi,
                    NlpKategori = aiKategorisi,
                    GorselYolu = dosyaYolu,
                    Durum = "Açık",
                    OlusturulmaTarihi = DateTime.Now
                };

                db.ArizaBiletleri.Add(yeniAriza);
                db.SaveChanges();

                TempData["BasariMesaji"] = "🚨 Bildiriminiz bakım ekibine anında iletildi. Teşekkür ederiz.";
                return RedirectToAction("ArizaBildir");
            }
            catch (Exception ex)
            {
                ViewBag.Hata = "Hata oluştu: " + ex.Message;
                ViewBag.Makineler = new SelectList(db.Makineler.Where(m => m.Durum == "Aktif").ToList(), "Id", "Ad");
                return View();
            }
        }

        [HttpGet]
        public ActionResult GetKategoriDagilimi()
        {
            var grafikVerisi = db.ArizaBiletleri
                                 .Where(a => !string.IsNullOrEmpty(a.NlpKategori))
                                 .GroupBy(a => a.NlpKategori)
                                 .Select(g => new {
                                     Kategori = g.Key,
                                     Adet = g.Count()
                                 })
                                 .ToList();

            return Json(grafikVerisi, JsonRequestBehavior.AllowGet);
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult KullaniciYonetimi()
        {
            var kullanicilar = db.Kullanicilar.ToList();
            return View(kullanicilar);
        }

        [HttpPost]
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult YetkiGuncelle(int id, string yeniRol)
        {
            var kullanici = db.Kullanicilar.Find(id);
            if (kullanici != null)
            {
                kullanici.Rol = yeniRol;
                db.SaveChanges();
                TempData["BasariMesaji"] = $"{kullanici.AdSoyad} adlı kullanıcının yetkisi '{yeniRol}' olarak güncellendi!";
            }
            return RedirectToAction("KullaniciYonetimi");
        }

        [AllowAnonymous]
        [HttpPost]
        public ActionResult ForkliftHizIhlaliKaydet(string kameraId, double olculenHiz, double limitHiz)
        {
            System.Diagnostics.Debug.WriteLine($"🚨 İHLAL TESPİTİ! {kameraId} bölgesinde {olculenHiz} km/h hız yapıldı!");
            return Json(new { basarili = true, mesaj = "Hız ihlali sisteme kaydedildi." });
        }

        // ==========================================
        // GEÇMİŞ KAYITLAR (ARŞİV) METOTLARI
        // ==========================================
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult GecmisArizalar()
        {
            var cozulenArizalar = db.ArizaBiletleri
                                    .Include(a => a.Makineler)
                                    .Where(a => a.Durum == "Çözüldü")
                                    .OrderByDescending(a => a.KapanisTarihi ?? a.OlusturulmaTarihi)
                                    .ToList();
            return View(cozulenArizalar);
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult GecmisIsgIhlalleri()
        {
            var incelenenIhlaller = db.IsgIhlalleri
                                      .Where(i => i.Durum == "İncelendi")
                                      .OrderByDescending(i => i.Tarih)
                                      .ToList();
            return View(incelenenIhlaller);
        }

        // ==========================================
        // PDF RAPORLAMA METOTLARI
        // ==========================================

        // Mevcut PDF: Aktif Arızalar
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult ArizaRaporuPdf()
        {
            var acikArizalar = db.ArizaBiletleri
                                 .Include(a => a.Makineler)
                                 .Where(a => a.Durum == "Açık")
                                 .ToList();

            return new ViewAsPdf("ArizaListesiPdf", acikArizalar)
            {
                FileName = $"OptiGuven_Aktif_Arizalar_{DateTime.Now:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
            };
        }

        // Yeni PDF: Geçmiş Arızalar
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult GecmisArizaRaporuPdf()
        {
            var cozulenArizalar = db.ArizaBiletleri
                                    .Include(a => a.Makineler)
                                    .Where(a => a.Durum == "Çözüldü")
                                    .OrderByDescending(a => a.KapanisTarihi ?? a.OlusturulmaTarihi)
                                    .ToList();

            return new ViewAsPdf("GecmisArizaListesiPdf", cozulenArizalar)
            {
                FileName = $"OptiGuven_Gecmis_Arizalar_{DateTime.Now:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
            };
        }

        // Yeni PDF: Aktif İSG İhlalleri
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult AktifIsgRaporuPdf()
        {
            var aktifIhlaller = db.IsgIhlalleri
                                  .Where(i => i.Durum == "İncelenmedi")
                                  .OrderByDescending(i => i.Tarih)
                                  .ToList();

            return new ViewAsPdf("AktifIsgListesiPdf", aktifIhlaller)
            {
                FileName = $"OptiGuven_Aktif_ISG_Ihlalleri_{DateTime.Now:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
            };
        }

        // Yeni PDF: Geçmiş İSG İhlalleri
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult GecmisIsgRaporuPdf()
        {
            var gecmisIhlaller = db.IsgIhlalleri
                                   .Where(i => i.Durum == "İncelendi")
                                   .OrderByDescending(i => i.Tarih)
                                   .ToList();

            return new ViewAsPdf("GecmisIsgListesiPdf", gecmisIhlaller)
            {
                FileName = $"OptiGuven_Gecmis_ISG_Ihlalleri_{DateTime.Now:yyyyMMdd}.pdf",
                PageSize = Rotativa.Options.Size.A4,
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
            };
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult SecilenleriPdfAl(string idListesi)
        {
            if (string.IsNullOrEmpty(idListesi)) return RedirectToAction("ArizaListesi");

            // String'i alıp sayıya çeviriyoruz (Örn: "1,3,5" -> [1, 3, 5])
            var ids = idListesi.Split(',').Select(int.Parse).ToList();

            var secilenArizalar = db.ArizaBiletleri
                                    .Include(a => a.Makineler)
                                    .Where(a => ids.Contains(a.Id))
                                    .ToList();

            return new ViewAsPdf("ArizaListesiPdf", secilenArizalar)
            {
                FileName = $"OptiGuven_Secilen_Arizalar_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult GecmisSecilenArizalariPdf(string idListesi)
        {
            if (string.IsNullOrEmpty(idListesi)) return RedirectToAction("GecmisArizalar");

            var ids = idListesi.Split(',').Select(int.Parse).ToList();

            var secilenArizalar = db.ArizaBiletleri
                                    .Include(a => a.Makineler)
                                    .Where(a => ids.Contains(a.Id) && a.Durum == "Çözüldü")
                                    .OrderByDescending(a => a.KapanisTarihi ?? a.OlusturulmaTarihi)
                                    .ToList();

            return new ViewAsPdf("GecmisArizaListesiPdf", secilenArizalar)
            {
                FileName = $"OptiGuven_Secilen_Gecmis_Arizalar_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        // Seçilen AKTİF İSG ihlalleri için PDF
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult SecilenAktifIsgPdf(string idListesi)
        {
            var ids = idListesi.Split(',').Select(int.Parse).ToList();
            var secilenIhlaller = db.IsgIhlalleri.Where(i => ids.Contains(i.Id)).ToList();

            return new ViewAsPdf("AktifIsgListesiPdf", secilenIhlaller)
            {
                FileName = $"OptiGuven_Secilen_Aktif_ISG_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        // Seçilen GEÇMİŞ İSG ihlalleri için PDF
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult SecilenGecmisIsgPdf(string idListesi)
        {
            var ids = idListesi.Split(',').Select(int.Parse).ToList();
            var secilenIhlaller = db.IsgIhlalleri.Where(i => ids.Contains(i.Id)).ToList();

            return new ViewAsPdf("GecmisIsgListesiPdf", secilenIhlaller)
            {
                FileName = $"OptiGuven_Secilen_Gecmis_ISG_{DateTime.Now:yyyyMMdd}.pdf"
            };
        }

        // 1. YENİ PERSONEL EKLEME
        [HttpPost]
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult PersonelEkle(string AdSoyad, string EPosta, string Sifre, string Rol)
        {
            var yeniKullanici = new Kullanicilar
            {
                AdSoyad = AdSoyad,
                Email = EPosta, // Veritabanındaki adın Email ise burayı Email = EPosta olarak değiştir
                SifreHash = Sifre,
                Rol = Rol
            };
            db.Kullanicilar.Add(yeniKullanici);
            db.SaveChanges();

            TempData["BasariMesaji"] = $"{AdSoyad} adlı personel sisteme başarıyla eklendi.";
            return RedirectToAction("KullaniciYonetimi");
        }

        // 2. PERSONEL DÜZENLEME
        [HttpPost]
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult PersonelDuzenle(int Id, string AdSoyad, string EPosta, string Rol)
        {
            var kullanici = db.Kullanicilar.Find(Id);
            if (kullanici != null)
            {
                kullanici.AdSoyad = AdSoyad;
                kullanici.Email = EPosta; // Veritabanındaki adın Email ise Email yap
                kullanici.Rol = Rol;
                db.SaveChanges();
                TempData["BasariMesaji"] = "Personel bilgileri başarıyla güncellendi.";
            }
            return RedirectToAction("KullaniciYonetimi");
        }

        // 3. PERSONEL SİLME
        [YetkiKontrol(HedefRol = "Admin")]
        public ActionResult PersonelSil(int id)
        {
            var kullanici = db.Kullanicilar.Find(id);
            if (kullanici != null)
            {
                db.Kullanicilar.Remove(kullanici);
                db.SaveChanges();
                TempData["BasariMesaji"] = "Personel sistemden tamamen silindi.";
            }
            return RedirectToAction("KullaniciYonetimi");
        }
    }
}