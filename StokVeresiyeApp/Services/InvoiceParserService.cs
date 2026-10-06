using System.Drawing;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using ZXing;
using ZXing.Windows.Compatibility;

namespace StokVeresiyeApp.Services;

public class ParsedInvoiceItem
{
    public int LineNo { get; set; } = 1;
    public string? Barcode { get; set; }
    public string? ItemCode { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; } = 1;
    public string Unit { get; set; } = "Adet";
    public double UnitPrice { get; set; }
    public double DiscountPercent { get; set; }
    public double DiscountAmount { get; set; }
    public double VatPercent { get; set; } = 20;
    public double VatAmount { get; set; }
    public double OtherTaxes { get; set; }
    public double LineTotal { get; set; }
}

public class ParsedInvoiceResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public string? IssueTime { get; set; } // Düzenleme Saati (14:35:20)
    public string? CustomizationId { get; set; } // Özelleştirme No (TR1.2)
    public string? Scenario { get; set; } // Senaryo (TICARIFATURA, TEMELFATURA vb.)
    public string? InvoiceKind { get; set; } // Fatura Tipi (SATIS, IADE vb.)
    public string? OrderNumber { get; set; } // Sipariş No
    public string? OrderDate { get; set; } // Sipariş Tarihi
    public string? RelatedStore { get; set; } // İlgili Kitabevi / Bayi
    public string? CargoId { get; set; } // Kargo ID / Kargo No
    public string? ReferenceNo { get; set; } // Referans No / ETTN
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierTaxNumber { get; set; } = string.Empty;
    public string SupplierTaxOffice { get; set; } = string.Empty;
    public string SupplierAddress { get; set; } = string.Empty;
    public double SubTotal { get; set; }
    public double VatTotal { get; set; }
    public double GrandTotal { get; set; }
    public List<ParsedInvoiceItem> Items { get; set; } = new();
    public string RawText { get; set; } = string.Empty;
    public string FileType { get; set; } = "PDF"; // PDF veya XML
}

public static class InvoiceParserService
{
    /// <summary>
    /// PDF veya XML faturayı otomatik algılayıp ayrıştırır.
    /// </summary>
    public static ParsedInvoiceResult ParseInvoiceFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new ParsedInvoiceResult { Success = false, ErrorMessage = "Dosya bulunamadı." };

        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".xml")
        {
            return ParseXmlInvoice(filePath);
        }
        else if (ext == ".pdf")
        {
            return ParsePdfInvoice(filePath);
        }
        else
        {
            return new ParsedInvoiceResult { Success = false, ErrorMessage = "Desteklenmeyen dosya formatı. Lütfen .pdf veya .xml fatura seçiniz." };
        }
    }

    /// <summary>
    /// E-Fatura UBL-TR XML formatını %100 doğrulukla ayrıştırır.
    /// </summary>
    public static ParsedInvoiceResult ParseXmlInvoice(string filePath)
    {
        var result = new ParsedInvoiceResult { FileType = "XML" };
        try
        {
            var doc = XDocument.Load(filePath);
            XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
            XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

            var root = doc.Root;
            if (root == null)
                return new ParsedInvoiceResult { Success = false, ErrorMessage = "XML dosyası boş veya geçersiz." };

            // 1. Fatura No & Meta Bilgileri
            result.InvoiceNumber = root.Element(cbc + "ID")?.Value?.Trim() ?? "";
            result.CustomizationId = root.Element(cbc + "CustomizationID")?.Value?.Trim();
            result.Scenario = root.Element(cbc + "ProfileID")?.Value?.Trim();
            result.InvoiceKind = root.Element(cbc + "InvoiceTypeCode")?.Value?.Trim();
            result.IssueTime = root.Element(cbc + "IssueTime")?.Value?.Trim();
            result.ReferenceNo = root.Element(cbc + "UUID")?.Value?.Trim();

            // Sipariş Bilgileri
            var orderRef = root.Element(cac + "OrderReference");
            if (orderRef != null)
            {
                result.OrderNumber = orderRef.Element(cbc + "ID")?.Value?.Trim();
                result.OrderDate = orderRef.Element(cbc + "IssueDate")?.Value?.Trim();
            }

            // Kargo / İrsaliye
            var despatchRef = root.Element(cac + "DespatchDocumentReference");
            if (despatchRef != null)
            {
                result.CargoId = despatchRef.Element(cbc + "ID")?.Value?.Trim();
            }

            // İlgili Kitabevi / Alıcı Cari
            var customer = root.Element(cac + "AccountingCustomerParty")?.Element(cac + "Party");
            if (customer != null)
            {
                result.RelatedStore = customer.Element(cac + "PartyName")?.Element(cbc + "Name")?.Value?.Trim();
            }

            // 2. Fatura Tarihi
            string? issueDateStr = root.Element(cbc + "IssueDate")?.Value;
            if (DateTime.TryParse(issueDateStr, out var d))
                result.InvoiceDate = d;

            // 3. Satıcı Firma Bilgileri (AccountingSupplierParty)
            var supplier = root.Element(cac + "AccountingSupplierParty")?.Element(cac + "Party");
            if (supplier != null)
            {
                result.SupplierName = supplier.Element(cac + "PartyName")?.Element(cbc + "Name")?.Value?.Trim()
                    ?? supplier.Element(cac + "Person")?.Element(cbc + "FirstName")?.Value + " " + supplier.Element(cac + "Person")?.Element(cbc + "FamilyName")?.Value
                    ?? "";

                var taxScheme = supplier.Element(cac + "PartyTaxScheme");
                result.SupplierTaxOffice = taxScheme?.Element(cac + "TaxScheme")?.Element(cbc + "Name")?.Value?.Trim() ?? "";

                // VKN / TCKN
                var idElem = supplier.Element(cac + "PartyIdentification")?.Element(cbc + "ID");
                result.SupplierTaxNumber = idElem?.Value?.Trim() ?? "";

                var postal = supplier.Element(cac + "PostalAddress");
                if (postal != null)
                {
                    string street = postal.Element(cbc + "StreetName")?.Value ?? "";
                    string city = postal.Element(cbc + "CityName")?.Value ?? "";
                    string district = postal.Element(cbc + "CitySubdivisionName")?.Value ?? "";
                    result.SupplierAddress = $"{street} {district}/{city}".Trim();
                }
            }

            // 4. Toplamlar
            var monetaryTotal = root.Element(cac + "LegalMonetaryTotal");
            if (monetaryTotal != null)
            {
                result.SubTotal = ParseDouble(monetaryTotal.Element(cbc + "LineExtensionAmount")?.Value);
                result.GrandTotal = ParseDouble(monetaryTotal.Element(cbc + "PayableAmount")?.Value);
            }

            var taxTotal = root.Element(cac + "TaxTotal");
            if (taxTotal != null)
            {
                result.VatTotal = ParseDouble(taxTotal.Element(cbc + "TaxAmount")?.Value);
            }

            // 5. Kalemler (Invoice Lines)
            var lines = root.Elements(cac + "InvoiceLine");
            int lineIdx = 1;
            foreach (var line in lines)
            {
                var item = new ParsedInvoiceItem { LineNo = lineIdx++ };
                var itemElem = line.Element(cac + "Item");
                item.ItemName = itemElem?.Element(cbc + "Name")?.Value?.Trim() ?? "Ürün";
                item.ItemCode = itemElem?.Element(cac + "SellersItemIdentification")?.Element(cbc + "ID")?.Value?.Trim();
                item.Barcode = itemElem?.Element(cac + "StandardItemIdentification")?.Element(cbc + "ID")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(item.Barcode))
                {
                    item.Barcode = itemElem?.Element(cac + "ManufacturersItemIdentification")?.Element(cbc + "ID")?.Value?.Trim()
                        ?? itemElem?.Element(cac + "BuyersItemIdentification")?.Element(cbc + "ID")?.Value?.Trim();
                }

                var qtyElem = line.Element(cbc + "InvoicedQuantity");
                item.Quantity = ParseDouble(qtyElem?.Value, 1);
                item.Unit = qtyElem?.Attribute("unitCode")?.Value ?? "Adet";

                item.UnitPrice = ParseDouble(line.Element(cac + "Price")?.Element(cbc + "PriceAmount")?.Value);
                item.LineTotal = ParseDouble(line.Element(cbc + "LineExtensionAmount")?.Value);

                var taxSubtotal = line.Element(cac + "TaxTotal")?.Element(cac + "TaxSubtotal");
                item.VatPercent = ParseDouble(taxSubtotal?.Element(cac + "TaxCategory")?.Element(cbc + "Percent")?.Value, 20);
                item.VatAmount = ParseDouble(taxSubtotal?.Element(cbc + "TaxAmount")?.Value);

                var allowance = line.Element(cac + "AllowanceCharge");
                if (allowance != null)
                {
                    item.DiscountAmount = ParseDouble(allowance.Element(cbc + "Amount")?.Value);
                    item.DiscountPercent = ParseDouble(allowance.Element(cbc + "MultiplierFactorNumeric")?.Value) * 100;
                }

                result.Items.Add(item);
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            return new ParsedInvoiceResult { Success = false, ErrorMessage = "XML e-Fatura ayrıştırma hatası: " + ex.Message };
        }
    }

    /// <summary>
    /// E-Fatura PDF formatından akıllı metin ve tablo ayrıştırma
    /// </summary>
    public static ParsedInvoiceResult ParsePdfInvoice(string filePath)
    {
        var result = new ParsedInvoiceResult { FileType = "PDF" };
        var orderedLines = new List<string>();
        var fullTextBuilder = new StringBuilder();
        var scannedPageBarcodes = new List<(string Barcode, BarcodeFormat Format, double YBottom, double YTop)>();

        try
        {
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    // Sayfadaki gömülü barkod ve QR kod resimlerini ZXing ile tara
                    var pageBarcodes = ScanBarcodesFromPdfPageImages(page);
                    scannedPageBarcodes.AddRange(pageBarcodes);

                    // Kelimeleri dikey Y koordinatına göre (yukarıdan aşağıya) ve aynı satırda soldan sağa sırala
                    var words = page.GetWords()
                        .OrderByDescending(w => w.BoundingBox.Bottom)
                        .ThenBy(w => w.BoundingBox.Left)
                        .ToList();

                    // Y koordinatına göre satır grupla (satır yükseklik toleransı 6.0 pt)
                    double? currentY = null;
                    var currentLineWords = new List<string>();

                    foreach (var word in words)
                    {
                        if (currentY == null || Math.Abs(currentY.Value - word.BoundingBox.Bottom) > 6.0)
                        {
                            if (currentLineWords.Count > 0)
                            {
                                string l = string.Join(" ", currentLineWords).Trim();
                                if (!string.IsNullOrWhiteSpace(l))
                                {
                                    orderedLines.Add(l);
                                    fullTextBuilder.AppendLine(l);
                                }
                                currentLineWords.Clear();
                            }
                            currentY = word.BoundingBox.Bottom;
                        }
                        currentLineWords.Add(word.Text);
                    }

                    if (currentLineWords.Count > 0)
                    {
                        string l = string.Join(" ", currentLineWords).Trim();
                        if (!string.IsNullOrWhiteSpace(l))
                        {
                            orderedLines.Add(l);
                            fullTextBuilder.AppendLine(l);
                        }
                    }
                }
            }

            string fullText = fullTextBuilder.ToString();
            result.RawText = fullText;

            if (string.IsNullOrWhiteSpace(fullText))
            {
                return new ParsedInvoiceResult { Success = false, ErrorMessage = "PDF metin katmanı okunamadı veya taranmış resim olabilir." };
            }

            // 1. Fatura Numarası Tespiti
            // Öncelik 1: "Fatura No:", "Fatura Numarası:", "İrsaliyeli Fatura No:", "Belge No:" vb. açıkça etiketlenmiş alan
            var matchExplicitNo = Regex.Match(fullText, @"(?:Fatura\s*No(?:su)?|Fatura\s*Numaras[ıi]|İrsaliyeli\s*Fatura\s*No|Belge\s*No(?:su)?|Fatura\s*Sıra\s*No)\s*[:\s-]*([A-Z0-9\-\/]{4,25})", RegexOptions.IgnoreCase);
            if (matchExplicitNo.Success && !string.IsNullOrWhiteSpace(matchExplicitNo.Groups[1].Value))
            {
                result.InvoiceNumber = matchExplicitNo.Groups[1].Value.Trim().ToUpperInvariant();
            }
            else
            {
                // Öncelik 2: GİB Standart 16 haneli e-Fatura No (örn. DYN2026000007273, EAA2026...)
                var matchInvoiceNo = Regex.Match(fullText, @"\b([A-Z0-9]{3}20[2-9][0-9]\d{9})\b", RegexOptions.IgnoreCase);
                if (matchInvoiceNo.Success)
                {
                    result.InvoiceNumber = matchInvoiceNo.Groups[1].Value.ToUpperInvariant();
                }
                else
                {
                    var matchAltNo = Regex.Match(fullText, @"(?:Fatura|Belge)\s*[:\s-]*([A-Z0-9-]{6,20})", RegexOptions.IgnoreCase);
                    if (matchAltNo.Success)
                        result.InvoiceNumber = matchAltNo.Groups[1].Value.Trim().ToUpperInvariant();
                }
            }

            // 2. Fatura Tarihi
            var matchDate = Regex.Match(fullText, @"(?:Fatura\s*Tarihi|Düzenleme\s*Tarihi|Tarih)\s*[:\s-]*([0-3]?\d[\.\/\-][0-1]?\d[\.\/\-]20\d{2})", RegexOptions.IgnoreCase);
            if (matchDate.Success)
            {
                string rawDate = matchDate.Groups[1].Value.Replace("/", ".").Replace("-", ".");
                if (DateTime.TryParseExact(rawDate, "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedD) ||
                    DateTime.TryParse(rawDate, out parsedD))
                {
                    result.InvoiceDate = parsedD;
                }
            }

            // 2.1. Düzenleme Saati
            var matchTime = Regex.Match(fullText, @"(?:Düzenleme\s*Saati|Fatura\s*Saati|Saat)\s*[:\s-]*([0-2]?\d:[0-5]\d(?::[0-5]\d)?)", RegexOptions.IgnoreCase);
            if (matchTime.Success)
            {
                result.IssueTime = matchTime.Groups[1].Value.Trim();
            }

            // 2.2. Özelleştirme No (TR1.2 vb.)
            var matchCust = Regex.Match(fullText, @"(?:Özelleştirme\s*No|Özelleştirme)\s*[:\s-]*([A-Za-z0-9\._-]+)", RegexOptions.IgnoreCase);
            if (matchCust.Success)
            {
                result.CustomizationId = matchCust.Groups[1].Value.Trim();
            }

            // 2.3. Senaryo (TICARIFATURA, TEMELFATURA, EARSIVFATURA vb.)
            var matchScen = Regex.Match(fullText, @"(?:Senaryo|Profil)\s*[:\s-]*([A-Za-z0-9ÇĞİÖŞÜçğıöşü\._-]+)", RegexOptions.IgnoreCase);
            if (matchScen.Success)
            {
                result.Scenario = matchScen.Groups[1].Value.Trim();
            }

            // 2.4. Fatura Tipi (SATIS, IADE, TEVKIFAT, ISTISNA vb.)
            var matchKind = Regex.Match(fullText, @"(?:Fatura\s*Tipi|Fatura\s*Türü|Fatura\s*Modeli)\s*[:\s-]*([A-Za-z0-9ÇĞİÖŞÜçğıöşü\._-]+)", RegexOptions.IgnoreCase);
            if (matchKind.Success)
            {
                result.InvoiceKind = matchKind.Groups[1].Value.Trim();
            }

            // 2.5. Sipariş No & Tarihi
            var matchOrdNo = Regex.Match(fullText, @"(?:Sipariş\s*No|Sipariş\s*Numaras[ıi])\s*[:\s-]*([A-Za-z0-9\._\/-]+)", RegexOptions.IgnoreCase);
            if (matchOrdNo.Success)
            {
                result.OrderNumber = matchOrdNo.Groups[1].Value.Trim();
            }

            var matchOrdDate = Regex.Match(fullText, @"(?:Sipariş\s*Tarihi)\s*[:\s-]*([0-3]?\d[\.\/\-][0-1]?\d[\.\/\-]20\d{2})", RegexOptions.IgnoreCase);
            if (matchOrdDate.Success)
            {
                result.OrderDate = matchOrdDate.Groups[1].Value.Trim();
            }

            // 2.6. İlgili Kitabevi / Alıcı Cari / Şube
            var matchStore = Regex.Match(fullText, @"(?:İlgili\s*Kitabevi|Kitabevi|Şube|Alıcı\s*Kitabevi)\s*[:\s-]*([A-Za-z0-9ÇĞİÖŞÜçğıöşü\s\.\-]{3,60}?)(?=\r|\n|Sipariş|Kargo|Tarih|$)", RegexOptions.IgnoreCase);
            if (matchStore.Success)
            {
                result.RelatedStore = matchStore.Groups[1].Value.Trim();
            }

            // 2.7. Kargo ID / Kargo Takip / Taşıyıcı
            var matchCargo = Regex.Match(fullText, @"(?:Kargo\s*ID|Kargo\s*Takip(?:\s*No)?|Kargo\s*No|Taşıyıcı\s*(?:No|ID)|Sevk\s*İrsaliye\s*No)\s*[:\s-]*([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
            if (matchCargo.Success)
            {
                result.CargoId = matchCargo.Groups[1].Value.Trim();
            }

            // 2.8. Referans No / ETTN
            var matchRef = Regex.Match(fullText, @"(?:Referans\s*No|ETTN|UUID)\s*[:\s-]*([A-Za-z0-9\-]{8,40})", RegexOptions.IgnoreCase);
            if (matchRef.Success)
            {
                result.ReferenceNo = matchRef.Groups[1].Value.Trim();
            }

            // 3. VKN / TCKN (10 veya 11 hane)
            var matchTax = Regex.Match(fullText, @"(?:VKN|TCKN|Vergi\s*Kimlik\s*No|Vergi\s*No)\s*[:\s-]*(\d{10,11})", RegexOptions.IgnoreCase);
            if (matchTax.Success)
            {
                result.SupplierTaxNumber = matchTax.Groups[1].Value;
            }

            // 4. Vergi Dairesi
            var matchTaxOffice = Regex.Match(fullText, @"(?:Vergi\s*Dairesi|V\.D\.)\s*[:\s-]*([A-Za-zÇĞİÖŞÜçğıöşü\s]{3,30}?)(?=\r|\n|VKN|TCKN|Tel|Adres|$)", RegexOptions.IgnoreCase);
            if (matchTaxOffice.Success)
            {
                result.SupplierTaxOffice = matchTaxOffice.Groups[1].Value.Trim();
            }

            // 5. Satıcı Firma Adı (Faturayı kesen firma)
            // Faturanın üst ilk 30 satırında yer alır, "Sayın/Alıcı/Müşteri/Toplam/Matrah" İÇERMEZ
            foreach (var line in orderedLines.Take(30))
            {
                string tr = line.Trim();
                if (tr.Length < 4 || tr.Length > 120) continue;

                if (Regex.IsMatch(tr, @"(Toplam|Matrah|KDV|Vergi|Tutar|Fatura|Sayın|Ödenecek|TL|IBAN|Banka|Bakiye|Müşteri|Alıcı|İmza|İrsaliye|Tel|Web|E-Posta|Kep|Sıra\s*No)", RegexOptions.IgnoreCase))
                    continue;

                // Firma ekleri
                if (Regex.IsMatch(tr, @"\b(SAN|TİC|LTD|ŞTİ|A\.Ş|ANONİM|LİMİTED|TİCARET|PAZARLAMA|GIDA|İTHALAT|İHRACAT|HİZMETLERİ|MARKET|MAĞAZA|TEKSTİL|KİMYA|OTOMOTİV|DAĞITIM|ELEKTRONİK)\b", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(tr, @"(SANAYİ|TİCARET|LİMİTED|ŞİRKETİ)", RegexOptions.IgnoreCase))
                {
                    result.SupplierName = tr.Substring(0, Math.Min(120, tr.Length)).Trim();
                    break;
                }
            }

            // 6. Tutarlar (Matrah, KDV, Ödenecek Tutar / Genel Toplam)
            var matchSub = Regex.Match(fullText, @"(?:Mal\s*Hizmet\s*Toplam\s*Tutarı|KDV\s*Matrahı|Vergi\s*Hariç\s*Tutar|Matrah|Ara\s*Toplam)\s*[:\s-]*([0-9\.,]+)", RegexOptions.IgnoreCase);
            if (matchSub.Success)
            {
                result.SubTotal = ParseDouble(matchSub.Groups[1].Value);
            }

            var matchVat = Regex.Match(fullText, @"(?:Hesaplanan\s*KDV|KDV\s*Tutarı|KDV\s*Toplamı|Toplam\s*KDV)\s*[:\s-]*([0-9\.,]+)", RegexOptions.IgnoreCase);
            if (matchVat.Success)
            {
                result.VatTotal = ParseDouble(matchVat.Groups[1].Value);
            }
            else if (result.SubTotal > 0)
            {
                result.VatTotal = Math.Round(result.SubTotal * 0.20, 2);
            }

            var matchGrand = Regex.Match(fullText, @"(?:Ödenecek\s*Tutar|Genel\s*Toplam|Vergiler\s*Dahil\s*Toplam)\s*[:\s-]*([0-9\.,]+)", RegexOptions.IgnoreCase);
            if (matchGrand.Success)
            {
                result.GrandTotal = ParseDouble(matchGrand.Groups[1].Value);
            }
            else if (result.SubTotal > 0)
            {
                result.GrandTotal = result.SubTotal + result.VatTotal;
            }

            // 7. Satır Kalemleri Tespiti (Sıra No, Mal Hizmet, Miktar, Birim, Fiyat, İskonto, KDV, Tutar)
            result.Items = ExtractItemsFromOrderedLines(orderedLines);

            // 7.1. PDF içindeki resimlerden çözülen çizgili barkod (Code 128, Code 39, EAN vb.)
            // Kullanıcı kuralı: Faturada görseldeki gibi bir barkod varsa tüm ürünler için bu barkod geçerlidir.
            // Yoksa hiçbir şekilde barkod alanına eklenmez (boş kalır).
            var docBarcode = scannedPageBarcodes
                .Where(b => b.Format != BarcodeFormat.QR_CODE && !string.IsNullOrWhiteSpace(b.Barcode))
                .Select(b => b.Barcode.Trim())
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(docBarcode))
            {
                foreach (var item in result.Items)
                {
                    if (string.IsNullOrWhiteSpace(item.Barcode))
                        item.Barcode = docBarcode;
                }
            }

            // 7.2. GİB QR Kodundan (eğer varsa ve faturada eksik bilgi kaldıysa) veri çek
            var qrCode = scannedPageBarcodes.FirstOrDefault(b => b.Format == BarcodeFormat.QR_CODE);
            if (!string.IsNullOrWhiteSpace(qrCode.Barcode))
            {
                ParseGibQrData(qrCode.Barcode, result);
            }

            // Eğer kalem bulunamadıysa ama faturada tutar varsa varsayılan 1 satır aç
            if (result.Items.Count == 0 && result.SubTotal > 0)
            {
                result.Items.Add(new ParsedInvoiceItem
                {
                    LineNo = 1,
                    ItemName = "Fatura Kalemi / Mal Alımı",
                    Quantity = 1,
                    Unit = "Adet",
                    UnitPrice = result.SubTotal,
                    VatPercent = result.SubTotal > 0 && result.VatTotal > 0 ? Math.Round((result.VatTotal / result.SubTotal) * 100) : 20,
                    VatAmount = result.VatTotal,
                    LineTotal = result.GrandTotal > 0 ? result.GrandTotal : (result.SubTotal + result.VatTotal)
                });
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            return new ParsedInvoiceResult
            {
                Success = false,
                ErrorMessage = "PDF okuma hatası: " + ex.Message,
                RawText = fullTextBuilder.ToString()
            };
        }
    }

    /// <summary>
    /// Satırlardan tablo kalemlerini (Mal Hizmet, Barkod, Miktar, Fiyat, KDV, İskonto) akıllıca ayrıştırır.
    /// <summary>
    /// Satırlardan tablo kalemlerini (Mal Hizmet, Barkod, Miktar, Fiyat, KDV, İskonto) akıllıca ayrıştırır.
    /// </summary>
    /// <summary>
    /// Satırlardan tablo kalemlerini (Mal Hizmet, Barkod, Miktar, Fiyat, KDV, İskonto) akıllıca ayrıştırır.
    /// Başlık ve alt toplam metinlerini filtreler, çok satırlı ürün açıklamalarını birleştirir.
    /// </summary>
    private static List<ParsedInvoiceItem> ExtractItemsFromOrderedLines(List<string> lines)
    {
        var items = new List<ParsedInvoiceItem>();
        int headerIdx = -1;
        int footerIdx = lines.Count;

        // 1. Tablo Başlangıç (Header) ve Bitiş (Footer) Sınırlarını Belirle
        for (int i = 0; i < lines.Count; i++)
        {
            string t = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(t)) continue;

            if (headerIdx == -1)
            {
                // Başlık Satırı Kontrolü:
                // "Ürünün Adı", "Mal ve Hizmet Açıklaması", "Mal / Hizmet", "Mal Hizmet", "Ürün EAN Kodu", "Barkod", "Birim Fiyat", "Sıra No"
                if (Regex.IsMatch(t, @"(Sıra\s*No|Ürünün\s*Adı|Ürün\s*Adı|Mal\s*(?:ve\s*)?Hizmet(?:\s*Açıklamas[ıi])?|Mal\s*\/|Ürün\s*Açıklamas[ıi]|Ürün\s*EAN|EAN\s*Kodu|Barkod|Birim\s*Fiyat)", RegexOptions.IgnoreCase) ||
                    (t.Contains("Sıra") && (t.Contains("Miktar") || t.Contains("Birim") || t.Contains("Fiyat") || t.Contains("Barkod") || t.Contains("EAN") || t.Contains("Hizmet"))))
                {
                    headerIdx = i;
                    // Eğer tablonun 2. başlık satırı varsa (örn: "No Tutarı Tutarı Oranı Tutarı" veya "Açıklaması Miktarı")
                    if (i + 1 < lines.Count && Regex.IsMatch(lines[i + 1].Trim(), @"^(No\b|Tutarı\b|Oranı\b|KDV\b|utarı\b|Açıklamas[ıi]\b|Miktar[ıi]\b|Fiyat[ıi]\b)", RegexOptions.IgnoreCase))
                    {
                        headerIdx = i + 1;
                    }
                }
            }
            else
            {
                // Tablo sonu tespiti (Alt toplamlar ve dipnotlar)
                if (Regex.IsMatch(t, @"(Mal\s*Hizmet\s*Toplam|KDV\s*Matrahı|Vergi\s*Hariç|Hesaplanan\s*KDV|Vergiler\s*Dahil\s*Toplam|Ödenecek\s*Tutar|Genel\s*Toplam|YALNIZ\b|İrsaliye\s*yerine|Sistem\s*No|Notlar\b|Dipnot\b)", RegexOptions.IgnoreCase))
                {
                    footerIdx = i;
                    break;
                }
            }
        }

        if (headerIdx == -1)
        {
            return items;
        }

        // 2. Tablo Satırlarını Topla
        var tableLines = lines.Skip(headerIdx + 1).Take(footerIdx - (headerIdx + 1)).ToList();

        // 3. Satırları Tek Tek Ürün Kalemleri Olarak Topla
        // Bir satır şu durumlarda YENİ ÜRÜN satırıdır:
        // a) Sıra No ile başlayan satırlar (örn: "1 9789751968531 Kuran Yolu..." veya "1 Kuran Yolu..." veya "1 URN-12...")
        // b) Doğrudan 13 haneli EAN/Barkod ile başlayan satırlar (örn: "9789751968531 Kuran Yolu...")
        var mergedRows = new List<string>();
        string currentMerged = "";

        foreach (var rawLine in tableLines)
        {
            string t = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(t) || t == "." || t == "-" || t == ":") continue;

            // Alt toplam veya kdv matrahı metin kalıntılarını durdur
            if (Regex.IsMatch(t, @"(Mal\s*Hizmet\s*Toplam|KDV\s*Matrahı|Vergi\s*Hariç|Hesaplanan\s*KDV|Vergiler\s*Dahil|Ödenecek\s*Tutar)", RegexOptions.IgnoreCase))
            {
                break;
            }

            // Başlık satırı kalıntılarını filtrele (örn: ikinci sayfa başlığı veya kolon etiketleri)
            bool isHeaderResidual = Regex.IsMatch(t, @"^(Sıra|No\b|Mal\s*(?:ve\s*)?Hizmet|Ürünün\s*Adı|Ürün\s*Adı|Mal\s*\/|Miktar|Birim|Fiyat|KDV|Tutar|Toplam|Matrah|Açıklamas[ıi])", RegexOptions.IgnoreCase);
            if (isHeaderResidual)
            {
                continue;
            }

            // Yeni ürün satırı başlangıcı:
            // 1. Sıra Numarası + (Harf, EAN/Barkod veya Kod): Örn: "1 978975...", "1 Kuran...", "2 8690...", "3 Kitap..."
            bool startsWithLineNo = Regex.IsMatch(t, @"^\d{1,3}\s+([A-Za-zÇĞİÖŞÜçğıöşü\(\[\{]|\d{8,14}\b|[A-Z0-9\-]{3,})");

            // 2. Doğrudan EAN/Barkod ile başlayan satırlar: "9789751968531 Kuran Yolu...", "8690123456789 Bisküvi..."
            bool startsWithEanBarcode = Regex.IsMatch(t, @"^(97[89]\d{10}|86[89]\d{10}|\d{13})\b");

            if (startsWithLineNo || startsWithEanBarcode)
            {
                if (!string.IsNullOrWhiteSpace(currentMerged))
                {
                    mergedRows.Add(currentMerged);
                }
                currentMerged = t;
            }
            else
            {
                // Bir önceki ürünün çok satırlı açıklama veya isim devamı (örn: alt satıra sarkan kitap başlığı)
                if (string.IsNullOrWhiteSpace(currentMerged))
                    currentMerged = t;
                else
                    currentMerged += " " + t;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentMerged))
        {
            mergedRows.Add(currentMerged);
        }

        // 4. Birleştirilmiş Satırları Ayrıştır
        int lineCounter = 1;
        foreach (var rowText in mergedRows)
        {
            var item = ParseSingleItemRow(rowText, lineCounter);
            if (item != null)
            {
                items.Add(item);
                lineCounter++;
            }
        }

        return items;
    }

    /// <summary>
    /// Tek bir ürün satır metninden Barkod, Ürün Kodu, Miktar, Birim, Fiyat, KDV ve Ürün Adını çözer.
    /// </summary>
    private static ParsedInvoiceItem? ParseSingleItemRow(string rowText, int defaultLineNo)
    {
        string w = rowText;

        // 1. Sıra No Tespiti (Satırın en başındaki sıra numarası: 1, 2, 3...)
        int sNo = defaultLineNo;
        var sMatch = Regex.Match(w.TrimStart(), @"^(\d{1,3})\b");
        if (sMatch.Success && int.TryParse(sMatch.Groups[1].Value, out int pNo))
        {
            sNo = pNo;
            w = Regex.Replace(w, @"^\s*" + pNo + @"\b", " ");
        }

        // 2. Barkod / Ürün EAN Kodu Tespiti (Tabloda 'Barkod' veya 'Ürün EAN Kodu' sütunundan gelen kodlar)
        string? barcode = null;

        // 2.1. Açık etiketli EAN / Barkod (Örn: Barkod: 978975..., EAN: 8690...)
        var bExplicitMatch = Regex.Match(w, @"(?:Barkod\s*No|Barkod|Ürün\s*EAN(?:\s*Kodu)?|EAN\s*Kodu|EAN|GTIN)\s*[:\s-]*([0-9]{8,14})", RegexOptions.IgnoreCase);
        if (bExplicitMatch.Success)
        {
            barcode = bExplicitMatch.Groups[1].Value.Trim();
            w = w.Remove(bExplicitMatch.Index, bExplicitMatch.Length);
        }
        else
        {
            // 2.2. 13 haneli standart EAN-13 (Kitap ISBN-13: 978/979..., Türkiye GS1: 868/869... veya genel 13 haneli EAN)
            var b13Match = Regex.Match(w, @"\b(97[89]\d{10}|86[89]\d{10}|[1-9]\d{12})\b");
            if (b13Match.Success)
            {
                barcode = b13Match.Groups[1].Value.Trim();
                w = Regex.Replace(w, @"\b" + Regex.Escape(barcode) + @"\b", " ");
            }
            else
            {
                // 2.3. 14 haneli GTIN veya 12 haneli UPC
                var bOtherMatch = Regex.Match(w, @"\b(\d{14}|\d{12})\b");
                if (bOtherMatch.Success)
                {
                    barcode = bOtherMatch.Groups[1].Value.Trim();
                    w = Regex.Replace(w, @"\b" + Regex.Escape(barcode) + @"\b", " ");
                }
            }
        }

        // 3. Ürün Kodu Tespiti (varsa)
        string? itemCode = null;
        var cMatch = Regex.Match(w, @"\b([A-Z0-9]{3,8}-[A-Z0-9]{2,8}|[A-Z]\d{6,10}[A-Z0-9]*)\b", RegexOptions.IgnoreCase);
        if (cMatch.Success)
        {
            itemCode = cMatch.Groups[1].Value;
            w = Regex.Replace(w, @"\b" + Regex.Escape(itemCode) + @"\b", " ");
        }

        // 4. Miktar & Birim Tespiti
        double qty = 1.0;
        string unit = "Adet";

        var unitMatch = Regex.Match(w, @"(?<qty>\d+(?:[\.,]\d+)?)\s*(?<unit>ADET|Adet|adet|ADT|Adt|adt|KG|Kg|kg|KOLİ|Koli|koli|PAKET|Paket|paket|LİTRE|Litre|Lt|lt|METRE|Metre|Mt|mt|KUTU|Kutu|kutu|ÇUVAL|Çuval|TON|Ton|ÇİFT|Çift|CIFT|Cift)\b", RegexOptions.IgnoreCase);
        if (unitMatch.Success)
        {
            qty = ParseDouble(unitMatch.Groups["qty"].Value, 1.0);
            unit = NormalizeUnit(unitMatch.Groups["unit"].Value);
            w = w.Remove(unitMatch.Index, unitMatch.Length);
        }
        else
        {
            var unitQtyMatch = Regex.Match(w, @"(?<unit>ADET|Adet|adet|ADT|Adt|adt|KG|Kg|kg|KOLİ|Koli|koli|PAKET|Paket|paket|LİTRE|Litre|Lt|lt|METRE|Metre|Mt|mt|KUTU|Kutu|kutu)\s+(?<qty>\d+(?:[\.,]\d+)?)\b", RegexOptions.IgnoreCase);
            if (unitQtyMatch.Success)
            {
                qty = ParseDouble(unitQtyMatch.Groups["qty"].Value, 1.0);
                unit = NormalizeUnit(unitQtyMatch.Groups["unit"].Value);
                w = w.Remove(unitQtyMatch.Index, unitQtyMatch.Length);
            }
        }


        // 5. İskonto % ve KDV % Oranlarının Ayrıştırılması
        double vatRate = 20.0;
        double discountPercent = 0.0;

        // 5.1. Açık etiketler (İsk %, İskonto %, KDV %, KDV)
        var iskLabelMatch = Regex.Match(w, @"(?:İsk(?:onto)?\s*%?|İsk\s*%)\s*[:\s-]*%?\s*(\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
        if (iskLabelMatch.Success)
        {
            discountPercent = ParseDouble(iskLabelMatch.Groups[1].Value, 0.0);
            w = w.Remove(iskLabelMatch.Index, iskLabelMatch.Length);
        }

        var kdvLabelMatch = Regex.Match(w, @"(?:KDV\s*%?|K\.D\.V\.?)\s*[:\s-]*%?\s*(\d+(?:[\.,]\d+)?)", RegexOptions.IgnoreCase);
        if (kdvLabelMatch.Success)
        {
            vatRate = ParseDouble(kdvLabelMatch.Groups[1].Value, 20.0);
            w = w.Remove(kdvLabelMatch.Index, kdvLabelMatch.Length);
        }

        // 5.2. Metinde kalan % işaretlerini analiz et
        var pctMatches = Regex.Matches(w, @"%\s*(\d+(?:[\.,]\d+)?)");
        if (pctMatches.Count == 1)
        {
            double val = ParseDouble(pctMatches[0].Groups[1].Value, 20.0);
            // Eğer %1, %10, %20 ise KDV'dir; %25, %30, %35, %40, %45, %50 ise İskonto'dur!
            if (discountPercent == 0 && (val >= 25 || (val > 20 && val != 10 && val != 1)))
            {
                discountPercent = val;
            }
            else
            {
                vatRate = val;
            }
            w = w.Remove(pctMatches[0].Index, pctMatches[0].Length);
        }
        else if (pctMatches.Count >= 2)
        {
            // Fatura tablosunda İskonto sütunu KDV'den önce gelir: [Birim Fiyat] [%35 İsk %] [%20 KDV %]
            double pct1 = ParseDouble(pctMatches[0].Groups[1].Value, 0.0);
            double pct2 = ParseDouble(pctMatches[1].Groups[1].Value, 20.0);

            if (pct2 == 1 || pct2 == 10 || pct2 == 20)
            {
                discountPercent = pct1;
                vatRate = pct2;
            }
            else if (pct1 == 1 || pct1 == 10 || pct1 == 20)
            {
                vatRate = pct1;
                discountPercent = pct2;
            }
            else
            {
                discountPercent = Math.Max(pct1, pct2);
                vatRate = Math.Min(pct1, pct2);
            }

            foreach (Match m in pctMatches)
            {
                w = Regex.Replace(w, Regex.Escape(m.Value), " ");
            }
        }

        // 6. Sayısal Değerleri (Fiyat, Tutar, İskonto) Ayıkla
        var numMatches = Regex.Matches(w, @"\b\d+(?:[\.,]\d+)?\b");
        var numbers = new List<double>();
        foreach (Match m in numMatches)
        {
            double v = ParseDouble(m.Value, -1);
            if (v >= 0) numbers.Add(v);
        }

        double unitPrice = 0.0;
        double lineTotal = 0.0;
        double discountAmount = 0.0;

        if (numbers.Count == 1)
        {
            unitPrice = numbers[0];
            lineTotal = qty * unitPrice;
        }
        else if (numbers.Count == 2)
        {
            unitPrice = Math.Min(numbers[0], numbers[1]);
            lineTotal = Math.Max(numbers[0], numbers[1]);
        }
        else if (numbers.Count >= 3)
        {
            // Doğrulama: candPrice * qty ≈ candTotal formülünü ara
            bool found = false;
            for (int i = 0; i < numbers.Count - 1; i++)
            {
                for (int j = i + 1; j < numbers.Count; j++)
                {
                    if (Math.Abs(numbers[i] * qty - numbers[j]) < 1.0)
                    {
                        unitPrice = numbers[i];
                        lineTotal = numbers[j];
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }

            if (!found)
            {
                unitPrice = numbers[0];
                lineTotal = numbers[numbers.Count - 1];

                // Ara sayılarda KDV oranı (%1, %10, %20) var mı?
                for (int k = 1; k < numbers.Count - 1; k++)
                {
                    if (numbers[k] == 1 || numbers[k] == 10 || numbers[k] == 20)
                    {
                        vatRate = numbers[k];
                    }
                }
            }
        }

        if (lineTotal <= 0 && unitPrice > 0) lineTotal = Math.Round(qty * unitPrice, 2);
        if (unitPrice <= 0 && lineTotal > 0 && qty > 0) unitPrice = Math.Round(lineTotal / qty, 2);

        double vatAmt = Math.Round(lineTotal * (vatRate / 100.0), 2);

        // 7. Ürün Adını Temizle (Ürünün Adı ve Mal ve Hizmet Açıklaması)
        string cleanName = Regex.Replace(w, @"\b\d+(?:[\.,]\d+)?\b", " ");
        cleanName = Regex.Replace(cleanName, @"\b(TL|₺|Adet|ADET|ADT|KG|Kg|Koli|Paket)\b", " ", RegexOptions.IgnoreCase);
        cleanName = Regex.Replace(cleanName, @"\b(Mal\s*(?:ve\s*)?Hizmet(?:\s*Açıklamas[ıi])?|Ürünün\s*Adı|Ürün\s*Adı|Mal\s*\/|Açıklamas[ıi])\b", " ", RegexOptions.IgnoreCase);
        cleanName = Regex.Replace(cleanName, @"[%*#_/:;]", " ");
        cleanName = Regex.Replace(cleanName, @"\s+", " ").Trim();

        if (cleanName.Length > 490) cleanName = cleanName.Substring(0, 490).Trim();

        // Eğer isim çok kısaysa veya sadece anlamsız karakterse satır geçerli değildir
        if (cleanName.Length < 2) return null;

        // "No utarı utarı", "KDV Matrahı" vb. kaçak kelimeler varsa satırı atla
        if (Regex.IsMatch(cleanName, @"(utarı|Matrahı|Oranı\s*utarı|Toplam\s*Tutarı)", RegexOptions.IgnoreCase))
        {
            return null;
        }

        return new ParsedInvoiceItem
        {
            LineNo = sNo,
            Barcode = barcode,
            ItemCode = itemCode,
            ItemName = cleanName,
            Quantity = qty,
            Unit = unit,
            UnitPrice = unitPrice,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            VatPercent = vatRate,
            VatAmount = vatAmt,
            OtherTaxes = 0,
            LineTotal = lineTotal
        };
    }

    private static string NormalizeUnit(string rawUnit)
    {
        if (string.IsNullOrWhiteSpace(rawUnit)) return "Adet";
        string u = rawUnit.Trim().ToUpperInvariant();
        if (u.StartsWith("AD")) return "Adet";
        if (u.StartsWith("KG")) return "KG";
        if (u.StartsWith("KOL")) return "Koli";
        if (u.StartsWith("PAK")) return "Paket";
        if (u.StartsWith("L") || u.StartsWith("LT")) return "Litre";
        if (u.StartsWith("M") || u.StartsWith("MT")) return "Metre";
        if (u.StartsWith("KUT")) return "Kutu";
        if (u.StartsWith("ÇUV") || u.StartsWith("CUV")) return "Çuval";
        if (u.StartsWith("TON")) return "Ton";
        if (u.StartsWith("ÇİF") || u.StartsWith("CIF")) return "Çift";
        return "Adet";
    }

    /// <summary>
    /// PDF sayfasındaki gömülü resimleri (1D Barkod, QR Kod, DataMatrix) ZXing ile tarar ve çözer.
    /// </summary>
    public static List<(string Barcode, BarcodeFormat Format, double YBottom, double YTop)> ScanBarcodesFromPdfPageImages(Page page)
    {
        var found = new List<(string Barcode, BarcodeFormat Format, double YBottom, double YTop)>();
        try
        {
            var reader = new ZXing.Windows.Compatibility.BarcodeReader
            {
                Options = new ZXing.Common.DecodingOptions
                {
                    PossibleFormats = new List<BarcodeFormat>
                    {
                        BarcodeFormat.EAN_13,
                        BarcodeFormat.EAN_8,
                        BarcodeFormat.CODE_128,
                        BarcodeFormat.CODE_39,
                        BarcodeFormat.UPC_A,
                        BarcodeFormat.UPC_E,
                        BarcodeFormat.QR_CODE,
                        BarcodeFormat.DATA_MATRIX
                    },
                    TryHarder = true
                }
            };

            foreach (var img in page.GetImages())
            {
                byte[]? imgBytes = null;
                if (img.TryGetPng(out byte[] png))
                {
                    imgBytes = png;
                }
                else if (img.RawBytes != null && img.RawBytes.Count > 0)
                {
                    imgBytes = img.RawBytes.ToArray();
                }

                if (imgBytes != null && imgBytes.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(imgBytes);
                        using var bmp = new Bitmap(ms);
                        var decodeResult = reader.Decode(bmp);
                        if (decodeResult != null && !string.IsNullOrWhiteSpace(decodeResult.Text))
                        {
                            found.Add((decodeResult.Text.Trim(), decodeResult.BarcodeFormat, img.Bounds.Bottom, img.Bounds.Top));
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        return found;
    }

    /// <summary>
    /// GİB e-Belge QR kod içeriğinden fatura no, VKN veya toplam tutar eksikse tamamlar.
    /// </summary>
    private static void ParseGibQrData(string qrText, ParsedInvoiceResult result)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(qrText)) return;

            // VKN / TCKN
            var vknMatch = Regex.Match(qrText, @"(?:vkn|tckn|vk|tc)[\s:=]+(\d{10,11})", RegexOptions.IgnoreCase);
            if (vknMatch.Success && string.IsNullOrWhiteSpace(result.SupplierTaxNumber))
            {
                result.SupplierTaxNumber = vknMatch.Groups[1].Value;
            }

            // Fatura No (GİB Standart 16 hane veya parametre)
            var fnMatch = Regex.Match(qrText, @"(?:fn|no|fatura|faturano)[\s:=]+([A-Z0-9-]{10,20})", RegexOptions.IgnoreCase);
            if (fnMatch.Success && string.IsNullOrWhiteSpace(result.InvoiceNumber))
            {
                result.InvoiceNumber = fnMatch.Groups[1].Value.ToUpperInvariant();
            }

            // Tutar
            var ttMatch = Regex.Match(qrText, @"(?:tt|toplam|tutar|amount)[\s:=]+([0-9\.,]+)", RegexOptions.IgnoreCase);
            if (ttMatch.Success && result.GrandTotal <= 0)
            {
                result.GrandTotal = ParseDouble(ttMatch.Groups[1].Value);
            }
        }
        catch { }
    }

    private static double ParseDouble(string? val, double fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(val)) return fallback;
        val = val.Trim();

        // 1.250,50 -> 1250.50
        if (val.Contains(".") && val.Contains(","))
        {
            int lastDot = val.LastIndexOf('.');
            int lastComma = val.LastIndexOf(',');
            if (lastComma > lastDot)
            {
                // Türk standardı: 1.250,50
                val = val.Replace(".", "").Replace(",", ".");
            }
            else
            {
                // İngiliz standardı: 1,250.50
                val = val.Replace(",", "");
            }
        }
        else if (val.Contains(","))
        {
            // 1250,50 -> 1250.50
            val = val.Replace(",", ".");
        }
        else if (val.Contains("."))
        {
            // 3 basamaklı binlik ayracı mı? (Örn: 1.250 veya 50.000)
            if (Regex.IsMatch(val, @"^\d{1,3}\.\d{3}$"))
            {
                val = val.Replace(".", "");
            }
        }

        if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var res))
            return res;

        return fallback;
    }
}

