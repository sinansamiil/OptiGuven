using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OptiGuven.Web.Tools
{
    public class SensorSimulasyon
    {
        private static readonly HttpClient client = new HttpClient();
        private static bool _calisiyorMu = false;

        // 1. GLOBAL.ASAX'IN ARADIĞI BAŞLAT METODU (Hatanın Çözümü)
        public static void Baslat()
        {
            // Eğer simülasyon zaten çalışıyorsa tekrar başlatma
            if (_calisiyorMu) return;
            _calisiyorMu = true;

            // Arka planda sonsuz bir döngüde sensör simülasyonunu çalıştırır
            Task.Run(async () =>
            {
                while (true)
                {
                    // İleride sensör verilerini otomatik üretip veritabanına yazdırma
                    // ve AnalizEt metodunu çağırma işlemlerini bu döngüye ekleyebiliriz.

                    await Task.Delay(10000); // 10 saniyede bir döner
                }
            });
        }

        // 2. YENİ EKLENEN PYTHON MAKİNE ÖĞRENMESİ (SCIKIT-LEARN) BAĞLANTISI
        public async Task<MLResult> AnalizEt(double vibration, double temperature, double workingHours)
        {
            var url = "http://127.0.0.1:8000/predict";

            var requestData = new
            {
                vibration = vibration,
                temperature = temperature,
                working_hours = workingHours
            };

            var json = JsonConvert.SerializeObject(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<MLResult>(responseString);
                }
            }
            catch (Exception)
            {
                // API kapalıysa sistem çökmesin diye varsayılan değer dönüyoruz
                return new MLResult { ariza_riski = 0, kalan_omur = "Hizmet Disi" };
            }

            return new MLResult { ariza_riski = 0, kalan_omur = "Belirsiz" };
        }
    }

    // Python'dan gelen JSON verisini karşılayan model sınıfı
    public class MLResult
    {
        public double ariza_riski { get; set; }
        public string kalan_omur { get; set; }
    }
}