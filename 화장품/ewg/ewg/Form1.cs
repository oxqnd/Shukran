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
            //timer1.Interval = 500;
            if (!(comboBox1.SelectedItem is Category selected)) return;

            string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36";
            string cfClearanceValue = "6RsCPjCF.Y6v3vbgi1kmoH52RzL3XOGTt.qGUCdG87o-1695961702-0-1-aaafc844.6a15e54a.be9119d1-0.2.1695961702";

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
                        productListFetchQueue.Enqueue(new KeyValuePair<string, string>($"{selected.cat}/{item.SubItems[0].Text}", sub.href + "?page=" + i));
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
                    string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36";
                    string cfClearanceValue = "6RsCPjCF.Y6v3vbgi1kmoH52RzL3XOGTt.qGUCdG87o-1695961702-0-1-aaafc844.6a15e54a.be9119d1-0.2.1695961702";

                    var html = await GetHtml(pdurl.Value, userAgent, cfClearanceValue);
                    var productInfo = await GetProductInfo(html);
                    productSaveQeueue.Enqueue(new KeyValuePair<string, ProductInfo>(pdurl.Key, productInfo));
                    Console.WriteLine($"{pdurl.Key}:{pdurl.Value}");
                }
                else if (productListFetchQueue.TryDequeue(out var pdlurl))
                {
                    string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36";
                    string cfClearanceValue = "6RsCPjCF.Y6v3vbgi1kmoH52RzL3XOGTt.qGUCdG87o-1695961702-0-1-aaafc844.6a15e54a.be9119d1-0.2.1695961702";

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
            catch
            {

            }
            finally
            {
                Interlocked.Decrement(ref threadCount);
            }
        }

        private async Task<ProductInfo> GetProductInfo(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var anchors = doc.QuerySelectorAll(".product-info-wrapper a[href]");

            var product = new ProductInfo();
            product.Category = anchors.Where(x => x.GetAttributeValue("href", "").Contains("/skindeep/browse/category/")).FirstOrDefault()?.InnerText.Trim();
            product.Brand = anchors.Where(x => x.GetAttributeValue("href", "").Contains("/skindeep/browse/brands/")).FirstOrDefault()?.InnerHtml.Trim();
            product.Name = doc.QuerySelector("h2.product-name.text-block")?.InnerHtml.Trim();
            product.EWG = doc.QuerySelector(".product-info-wrapper .product-score img")?.GetAttributeValue("alt", "");
            product.EWG = Regex.Match(product.EWG, @"(\d+|EWG Verified)").Groups[1].Value ?? product.EWG;
            var concern = doc.QuerySelectorAll(".product-info-wrapper .concern");
            if (concern.Count > 0)
            {
                product.Cancer = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Cancer").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
                product.Allergies = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Allergies & Immunotoxicity").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
                product.Toxicity = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Developmental and Reproductive Toxicity").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();
                product.Restriction = concern.Where(x => x.QuerySelector(".concern-text").InnerText == "Use Restrictions").FirstOrDefault()?.QuerySelector(".level").InnerText.Trim();

            }
            product.Label = doc.QuerySelector("#label-information").InnerText.Trim();
            product.Certification = string.Join(",", doc.QuerySelectorAll("#animal-testing-policies h2").Select(x => x.InnerText.Trim()));

            return product;
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            //string url = "https://www.ewg.org/skindeep/browse/category/Blush/?page=3";
            string url = "https://www.ewg.org/skindeep/browse/category/Blush/";
            string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36";
            string cfClearanceValue = "6RsCPjCF.Y6v3vbgi1kmoH52RzL3XOGTt.qGUCdG87o-1695961702-0-1-aaafc844.6a15e54a.be9119d1-0.2.1695961702";

            string html = await GetHtml(url, userAgent, cfClearanceValue);

            var max = await GetMaxPage(html);
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

        /*
        private async Task<string> GetHtml(string url)
        {
            var httpClient = new HttpClient();

            var response = await httpClient.GetAsync(url);
            var html = await response.Content.ReadAsStringAsync();
            return html;
        }
        */
        /*
        // 1차 수정
        private async Task<int> GetMaxPage(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var anchors = doc.QuerySelectorAll("a[href]");
            return anchors.Where(x => x.GetAttributeValue("href", "").Contains("page=")).Select(x => int.Parse(new Regex(@"page=(\d+)").Match(x.GetAttributeValue("href", "")).Groups[1].Value)).Max();
        }
        */
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
            return tile.Select(x => x.QuerySelector("a").GetAttributeValue("href", "")).ToList();
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
        public string Brand { get; set; }
        public string Name { get; set; }
        public string EWG { get; set; }
        public string Category { get; set; }
        public string Cancer { get; set; }
        public string Allergies { get; set; }
        public string Toxicity { get; set; }
        public string Restriction { get; set; }
        public string Label { get; set; }
        public string Certification { get; set; }
    }
#pragma warning restore IDE1006 // 명명 스타일
}
