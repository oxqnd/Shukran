using MetroFramework.Forms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HtmlAgilityPack;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using System.Text.RegularExpressions;
using HtmlAgilityPack.CssSelectors.NetCore;
using System.Collections.Concurrent;
using System.Threading;
using CsvHelper;
using CsvHelper.Configuration;
using System.Runtime.InteropServices;
using System.Diagnostics;


namespace EWG_Scraper
{
    public partial class Form1 : MetroForm
    {
        private List<Category> categories;
        private ConcurrentQueue<KeyValuePair<string, string>> productListFetchQueue = new ConcurrentQueue<KeyValuePair<string, string>>();
        private ConcurrentQueue<KeyValuePair<string, string>> productFetchQueue = new ConcurrentQueue<KeyValuePair<string, string>>();
        private ConcurrentQueue<KeyValuePair<string, ProductInfo>> productSaveQeueue = new ConcurrentQueue<KeyValuePair<string, ProductInfo>>();
        private int threadCount = 0;
        private const int MAX_THREAD = 2;
        private static int currentIndex = 0;
        private const string clear = "mGX_jODzuL.duLTLUJzKKiQII7TCwzB4nVoCGveBRSQ-1698369213-0-1-aa43f037.806e8560.fd42afd7-0.2.1698369213";
        private const string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36";

        public Form1()
        {
            InitializeComponent();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            categories = JsonConvert.DeserializeObject<List<Category>>(File.ReadAllText(@"category.json"));
            comboBox1.Items.AddRange(categories.ToArray());
            //AllocConsole();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(comboBox1.SelectedItem is Category selected)) return;

            listView1.Items.Clear();

            if (!Directory.Exists(selected.cat)) Directory.CreateDirectory(selected.cat);

            foreach (var item in selected.sub)
            {
                var path = $@"{selected.cat}\{item.cat}.csv";
                var done = File.Exists(path);

                listView1.Items.Add(new ListViewItem(new[] { item.cat, done.ToString() }));
            }
        }
        private async void button1_Click(object sender, EventArgs e)
        {
            if (!(comboBox1.SelectedItem is Category selected)) return;

            string userAgent = useragent;
            string cfClearanceValue = clear;

            foreach (ListViewItem item in listView1.Items)
            {
                if (item.SubItems[1].Text != "False" || !selected.sub.Any(x => item.SubItems[0].Text == x.cat))
                {
                    Console.WriteLine($"{item.SubItems[0].Text}는 건너뜀.");
                }
                else
                {
                    var sub = selected.sub.Find(x => x.cat == item.SubItems[0].Text);

                    var firstPage = await GetHtml(sub.href, userAgent, cfClearanceValue);
                    var maxPage = await GetMaxPage(firstPage);

                    Console.WriteLine($"{selected.cat}/{item.SubItems[0].Text} {maxPage}페이지 예약됨.");
                    for (int i = 1; i <= maxPage; i++)
                    {
                        productListFetchQueue.Enqueue(new KeyValuePair<string, string>($"{selected.cat}/{item.SubItems[0].Text}", sub.href + (sub.href.IndexOf("?") != -1 ? "&" : "?") + "page=" + i));
                    }
                }

            }
            timer1.Start();
        }

        private readonly object filelock = new object();
        private async Task Work()
        {
            if (threadCount > MAX_THREAD) return;
            Interlocked.Increment(ref threadCount);
            Debug.WriteLine("Work()");
            try
            {
                if (productSaveQeueue.TryDequeue(out var save))
                {
                    lock (filelock)
                    {
                        var path = save.Key.Replace("/", @"\") + ".csv";
                        var first = !File.Exists(path); // 파일이 없을 때만 헤더를 쓰도록 수정

                        var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture) { HasHeaderRecord = first };
                        using (var stream = File.Open(path, FileMode.Append))
                        using (var writer = new StreamWriter(stream))
                        using (var csv = new CsvWriter(writer, config))
                        {
                            if (first)
                            {
                                csv.WriteHeader<ProductInfo>();
                                csv.NextRecord();
                            }
                            csv.WriteRecord(save.Value);
                            csv.NextRecord();
                        }
                    }
                }
                else if (productFetchQueue.TryDequeue(out var pdurl))
                {
                    string userAgent = useragent;
                    string cfClearanceValue = clear;

                    string html = null;
                    while ((html = await GetHtml(pdurl.Value, userAgent, cfClearanceValue)) == null) Thread.Sleep(1000);

                    // URL과 HTML 컨텐츠를 함께 넘깁니다.
                    var productInfo = await GetProductInfo(pdurl.Value, html);

                    productSaveQeueue.Enqueue(new KeyValuePair<string, ProductInfo>(pdurl.Key, productInfo));
                    Console.WriteLine($"{pdurl.Key}:{pdurl.Value}");
                }

                else if (productListFetchQueue.TryDequeue(out var pdlurl))
                {
                    string userAgent = useragent;
                    string cfClearanceValue = clear;

                    var html = await GetHtml(pdlurl.Value, userAgent, cfClearanceValue);
                    var productList = await GetProductList(html);
                    for (int i = 0; i < productList.Count; i++)
                    {
                        productFetchQueue.Enqueue(new KeyValuePair<string, string>(pdlurl.Key, productList[i]));
                    }
                }
                else
                {
                    Console.WriteLine("완료");
                }
            }
            finally
            {
                Interlocked.Decrement(ref threadCount);
            }
        }

        private async Task<ProductInfo> GetProductInfo(string url, string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var anchors = doc.QuerySelectorAll(".product-info-wrapper a[href]");

            var product = new ProductInfo();

            product.ID = $"I_{currentIndex}";
            currentIndex++;

            //product.Name = doc.QuerySelector("h2.product-name.text-block")?.InnerHtml.Trim();
            product.Name = doc.QuerySelector("head > title")?.InnerHtml.Trim().Split(new string[] { "What is " }, StringSplitOptions.None)[1];
            // URL에서 ID 추출
            var uri = new Uri(url);
            var segments = uri.Segments;
            if (segments.Length > 3)
            {
                // URL의 마지막 부분에서 '-' 기준으로 앞부분만 추출
                var idPart = segments[3].TrimEnd('/');
                var idSplit = idPart.Split('-');
                if (idSplit.Length >= 1)
                {
                    product.EWG_ID = idSplit[0]; // '-' 앞 부분만 저장
                }
            }

            var descriptionElement = doc.QuerySelector(".chemical-info.chemical-about-text");
            if (descriptionElement != null)
            {
                product.Description = System.Net.WebUtility.HtmlDecode(descriptionElement.InnerHtml.Trim());
            }

            var ewgScoreElement = doc.QuerySelector(".product-score img");
            if (ewgScoreElement != null)
            {
                string scoreAttribute = ewgScoreElement.GetAttributeValue("src", "");
                var match = Regex.Match(scoreAttribute, @"score=(\d+)&amp;score_min=(\d+)");

                if (match.Success)
                {
                    product.ScoreMin = match.Groups[2].Value; // score_min 값
                    product.ScoreMax = match.Groups[1].Value; // score 값

                    if (product.ScoreMax == product.ScoreMin)
                    {
                        product.EWG = product.ScoreMax; // score와 scoreMin이 같을 때, 하나만 사용
                    }
                    else
                    {
                        string ewgScore = $"{product.ScoreMin}_{product.ScoreMax}";
                        product.EWG = ewgScore;
                    }
                }
                else
                {
                    Console.WriteLine("패턴 매칭 실패");
                }
            }

            var concern = doc.QuerySelectorAll(".product-info-wrapper .concern");
            if (concern.Count > 0)
            {
                product.Cancer = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Cancer").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
                product.Allergies = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Allergies & Immunotoxicity").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
                product.Toxicity = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Developmental and Reproductive Toxicity").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
                product.Restriction = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Use Restrictions").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
            }
            return product;
        }

        private async Task<string> GetHtml(string url, string userAgent, string cfClearanceValue)
        {
            try
            {
                var httpClient = new HttpClient();

                // cf_clearance 및 User-Agent 헤더 설정
                httpClient.DefaultRequestHeaders.Add("cf_clearance", cfClearanceValue);
                httpClient.DefaultRequestHeaders.Add("User-Agent", userAgent);

                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    Console.WriteLine($"HTTP Error: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetHtml: {ex.Message}");
                return null;
            }
        }
        private async Task<int> GetMaxPage(string html)
        {
            if (html == null)
            {
                throw new ArgumentNullException(nameof(html), "html 매개 변수는 null일 수 없습니다.");
            }
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var anchors = doc.QuerySelectorAll("a[href]");
            var pageNumbers = anchors
                .Where(x => x.GetAttributeValue("href", null)?.Contains("page=") ?? false)
                .Select(x =>
                {
                    var match = new Regex(@"page=(\d+)").Match(x.GetAttributeValue("href", ""));
                    return match.Success ? int.Parse(match.Groups[1].Value) : 0;
                });

            return pageNumbers.Any() ? pageNumbers.Max() : 1;
        }

        private async Task<List<string>> GetProductList(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tile = doc.QuerySelectorAll(".product-tile");

            var list = tile.Select(x => x.QuerySelector("a").GetAttributeValue("href", ""));
            return list.Select(x =>
            {
                if (x.StartsWith("https")) return x;
                return new Uri(new Uri("https://www.ewg.org/"), x).ToString();
            }).ToList();
        }

        private async void timer1_Tick(object sender, EventArgs e)
        {
            _ = Task.Run(Work);
        }
    }

#pragma warning disable IDE1006 // 명명 스타일
    public class Category
    {
        public string cat { get; set; }
        public List<SubCategory> sub { get; set; }

        public override string ToString()
        {
            return cat;
        }
    }
    public class SubCategory
    {
        public string cat { get; set; }
        public string href { get; set; }
    }
    public class ProductInfo
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string EWG_ID { get; set; }
        public string EWG { get; set; }
        public string ScoreMin { get; set; }
        public string ScoreMax { get; set; }
        public string Description { get; set; }
        public string Cancer { get; set; }
        public string Allergies { get; set; }
        public string Toxicity { get; set; }
        public string Restriction { get; set; }
    }
#pragma warning restore IDE1006 // 명명 스타일
}