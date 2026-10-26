using System;
using System.Net;
using System.Net.Mail;

namespace OptiGuven.Web.Helpers
{
    public static class MailServisi
    {
        public static void AcilDurumMailiGonder(string mesaj)
        {
            try
            {
                // Gmail adresiniz ve Google Uygulama Şifreniz buraya gelecek (örn. Web.config veya ortam değişkeni)
                var gondericiMail = new MailAddress("sistem@optiguven.com", "OptiGüven Uyarı Sistemi");
                var aliciMail = new MailAddress("yonetici@optiguven.com", "Saha Yöneticisi");
                var sifre = "BURAYA_GMAIL_UYGULAMA_SIFRESI_GELECEK";

                var konu = "🚨 KRİTİK UYARI: OptiGüven Anomali Tespiti";
                var icerik = $@"
                    <div style='border:2px solid red; padding:20px; border-radius:10px; font-family:Arial;'>
                        <h2 style='color:red;'>⚠️ OptiGüven Acil Durum Bildirimi</h2>
                        <p style='font-size:16px;'>{mesaj}</p>
                        <hr/>
                        <p><strong>Tarih:</strong> {DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")}</p>
                        <p>Lütfen derhal makine kontrol panelini ve sahayı inceleyiniz.</p>
                    </div>";

                var smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true, // Güvenli bağlantı şart
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(gondericiMail.Address, sifre)
                };

                using (var mail = new MailMessage(gondericiMail, aliciMail)
                {
                    Subject = konu,
                    Body = icerik,
                    IsBodyHtml = true
                })
                {
                    smtp.Send(mail);
                }
            }
            catch (Exception ex)
            {
                // Mail gönderilemezse sistemin çökmesini engelleriz (Try-Catch hayat kurtarır)
                Console.WriteLine("Mail Hatası: " + ex.Message);
            }
        }
    }
}