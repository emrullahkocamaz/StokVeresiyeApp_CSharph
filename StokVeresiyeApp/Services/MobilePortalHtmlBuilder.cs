namespace StokVeresiyeApp.Services;

public static class MobilePortalHtmlBuilder
{
    public static string GetHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""tr"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <meta name=""apple-mobile-web-app-capable"" content=""yes"">
    <meta name=""apple-mobile-web-app-status-bar-style"" content=""black-translucent"">
    <title>Bilensis Mobil Yönetici & İşlem Portalı</title>
    <script src=""https://unpkg.com/@zxing/library@latest/umd/index.min.js""></script>
    <style>
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: #0f172a;
            color: #f8fafc;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            padding-bottom: 68px;
        }
        header {
            background: linear-gradient(135deg, #1e293b, #0f172a);
            padding: 12px 16px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            border-bottom: 1px solid #334155;
            position: sticky;
            top: 0;
            z-index: 50;
        }
        .brand { display: flex; align-items: center; gap: 8px; font-weight: 700; font-size: 1.05rem; color: #38bdf8; }
        .badge-live {
            background: #10b981;
            color: #fff;
            padding: 4px 10px;
            border-radius: 9999px;
            font-size: 0.72rem;
            font-weight: 600;
            display: inline-flex;
            align-items: center;
            gap: 5px;
        }
        .badge-live::before {
            content: '';
            width: 6px;
            height: 6px;
            background: #fff;
            border-radius: 50%;
            animation: pulse 1.5s infinite;
        }
        @keyframes pulse { 0% { opacity: 1; transform: scale(1); } 50% { opacity: 0.4; transform: scale(1.3); } 100% { opacity: 1; transform: scale(1); } }

        nav.bottom-nav {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            height: 62px;
            background: #1e293b;
            border-top: 1px solid #334155;
            display: flex;
            justify-content: space-around;
            align-items: center;
            z-index: 100;
            backdrop-filter: blur(12px);
        }
        .nav-item {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            color: #94a3b8;
            font-size: 0.72rem;
            font-weight: 600;
            cursor: pointer;
            width: 25%;
            height: 100%;
            transition: all 0.2s;
            border: none;
            background: transparent;
        }
        .nav-item.active {
            color: #38bdf8;
        }
        .nav-item span.icon {
            font-size: 1.25rem;
            margin-bottom: 2px;
        }

        .view-section {
            display: none;
            padding: 14px;
            flex-direction: column;
            gap: 12px;
            animation: fadeIn 0.25s ease-out;
        }
        .view-section.active {
            display: flex;
        }
        @keyframes fadeIn { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: translateY(0); } }

        .card {
            background: #1e293b;
            border-radius: 12px;
            padding: 16px;
            border: 1px solid #334155;
            box-shadow: 0 4px 14px rgba(0,0,0,0.25);
        }
        .card-hero {
            background: linear-gradient(135deg, #1e3a8a, #0f172a);
            border: 1px solid #3b82f6;
        }
        .card-title {
            font-size: 0.8rem;
            color: #94a3b8;
            text-transform: uppercase;
            font-weight: 700;
            letter-spacing: 0.5px;
            margin-bottom: 6px;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        .hero-amount {
            font-size: 2rem;
            font-weight: 800;
            color: #38bdf8;
            margin-bottom: 4px;
        }
        .grid-2 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 10px;
        }
        .stat-mini {
            background: #0f172a;
            border: 1px solid #334155;
            padding: 12px;
            border-radius: 10px;
        }
        .stat-mini-val {
            font-size: 1.2rem;
            font-weight: 700;
            margin-top: 4px;
        }

        .camera-container {
            position: relative;
            width: 100%;
            height: 42vh;
            background: #000;
            border-radius: 12px;
            overflow: hidden;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        video {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }
        .reticle {
            width: 78%;
            max-width: 320px;
            height: 160px;
            border: 2px solid rgba(255, 255, 255, 0.4);
            border-radius: 12px;
            position: absolute;
            box-shadow: 0 0 0 9999px rgba(0, 0, 0, 0.5);
        }
        .reticle::before {
            content: '';
            position: absolute;
            left: 0; right: 0;
            height: 2px;
            background: #10b981;
            box-shadow: 0 0 8px #10b981;
            animation: scanLine 2s infinite ease-in-out;
        }
        @keyframes scanLine { 0% { top: 5%; } 50% { top: 90%; } 100% { top: 5%; } }
        .cam-controls {
            position: absolute;
            bottom: 10px;
            display: flex;
            gap: 10px;
            z-index: 10;
        }
        .btn-ctrl {
            background: rgba(15, 23, 42, 0.85);
            border: 1px solid #475569;
            color: #fff;
            padding: 6px 12px;
            border-radius: 20px;
            font-size: 0.8rem;
            cursor: pointer;
        }

        .search-bar {
            display: flex;
            gap: 8px;
        }
        .search-bar input {
            flex: 1;
            background: #1e293b;
            border: 1px solid #475569;
            color: #fff;
            padding: 10px 14px;
            border-radius: 8px;
            font-size: 0.95rem;
            outline: none;
        }
        .search-bar button {
            background: #38bdf8;
            color: #0f172a;
            border: none;
            padding: 0 16px;
            border-radius: 8px;
            font-weight: 700;
            cursor: pointer;
        }

        .form-group {
            display: flex;
            flex-direction: column;
            gap: 6px;
            margin-bottom: 12px;
        }
        .form-label {
            font-size: 0.82rem;
            color: #cbd5e1;
            font-weight: 600;
        }
        .form-input, .form-select {
            background: #0f172a;
            border: 1px solid #475569;
            color: #fff;
            padding: 12px 14px;
            border-radius: 8px;
            font-size: 1rem;
            outline: none;
        }
        .form-input:focus, .form-select:focus {
            border-color: #38bdf8;
        }
        .btn-primary {
            background: linear-gradient(135deg, #10b981, #059669);
            color: #fff;
            padding: 14px;
            border: none;
            border-radius: 10px;
            font-size: 1rem;
            font-weight: 700;
            cursor: pointer;
            width: 100%;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            box-shadow: 0 4px 12px rgba(16, 185, 129, 0.4);
        }
        .btn-primary:active { transform: scale(0.98); }

        .debtor-item {
            background: #1e293b;
            border: 1px solid #334155;
            padding: 14px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 10px;
        }
        .debtor-name { font-weight: 700; font-size: 0.95rem; color: #f8fafc; }
        .debtor-phone { font-size: 0.8rem; color: #94a3b8; margin-top: 2px; }
        .debtor-bal { font-size: 1.1rem; font-weight: 800; color: #f87171; text-align: right; }
        .btn-wa-mini {
            background: #25d366;
            color: #fff;
            border: none;
            padding: 6px 10px;
            border-radius: 6px;
            font-size: 0.78rem;
            font-weight: 700;
            display: inline-flex;
            align-items: center;
            gap: 4px;
            cursor: pointer;
            text-decoration: none;
            margin-top: 4px;
        }

        #toast {
            position: fixed;
            top: 70px;
            left: 50%;
            transform: translateX(-50%);
            background: #10b981;
            color: #fff;
            padding: 10px 20px;
            border-radius: 30px;
            font-weight: 700;
            font-size: 0.9rem;
            box-shadow: 0 4px 20px rgba(0,0,0,0.5);
            display: none;
            z-index: 200;
            animation: dropDown 0.3s ease-out;
        }
        @keyframes dropDown { from { top: 40px; opacity: 0; } to { top: 70px; opacity: 1; } }
    </style>
</head>
<body>
    <header>
        <div class=""brand"">⚡ Bilensis Mobil Portal</div>
        <div class=""badge-live"">🟢 Canlı Bağlantı</div>
    </header>

    <div id=""toast"">İşlem Başarılı!</div>

    <!-- 1. SEKME: KASA & CİRO -->
    <div id=""view-kasa"" class=""view-section active"">
        <div class=""card card-hero"">
            <div class=""card-title"">
                <span>📊 BUGÜNKÜ TOPLAM CİRO</span>
                <span id=""lblKasaTime"" style=""color:#93c5fd;font-weight:normal;"">...</span>
            </div>
            <div class=""hero-amount"" id=""lblHeroCiro"">0,00 ₺</div>
            <div style=""color:#bfdbfe;font-size:0.85rem;"">
                Satış Adedi: <strong id=""lblHeroSalesCount"">0</strong> fiş/fatura
            </div>
        </div>

        <div class=""grid-2"">
            <div class=""stat-mini"">
                <div style=""color:#94a3b8;font-size:0.75rem;"">💵 NAKİT SATIŞ</div>
                <div class=""stat-mini-val"" style=""color:#34d399;"" id=""lblCashSale"">0,00 ₺</div>
            </div>
            <div class=""stat-mini"">
                <div style=""color:#94a3b8;font-size:0.75rem;"">💳 KREDİ KARTI</div>
                <div class=""stat-mini-val"" style=""color:#60a5fa;"" id=""lblPosSale"">0,00 ₺</div>
            </div>
            <div class=""stat-mini"">
                <div style=""color:#94a3b8;font-size:0.75rem;"">📝 VERESİYE SATIŞ</div>
                <div class=""stat-mini-val"" style=""color:#fbbf24;"" id=""lblCreditSale"">0,00 ₺</div>
            </div>
            <div class=""stat-mini"">
                <div style=""color:#94a3b8;font-size:0.75rem;"">📥 TAHSİLAT</div>
                <div class=""stat-mini-val"" style=""color:#a78bfa;"" id=""lblTotalCol"">0,00 ₺</div>
            </div>
        </div>

        <div class=""card"">
            <div class=""card-title"">🏦 MERKEZ KASA NAKİT DURUMU</div>
            <div style=""font-size:1.6rem;font-weight:800;color:#10b981;"" id=""lblNetCash"">0,00 ₺</div>
            <div style=""color:#94a3b8;font-size:0.78rem;margin-top:4px;"">Kasada bulunması gereken net nakit</div>
        </div>

        <div class=""grid-2"">
            <div class=""stat-mini"" style=""border-color:#f59e0b;"">
                <div style=""color:#fbbf24;font-size:0.75rem;"">⚠️ KRİTİK STOK</div>
                <div class=""stat-mini-val"" style=""color:#fbbf24;"" id=""lblCritCount"">0 Ürün</div>
            </div>
            <div class=""stat-mini"" style=""border-color:#ef4444;"">
                <div style=""color:#f87171;font-size:0.75rem;"">👥 TOPLAM ALACAK</div>
                <div class=""stat-mini-val"" style=""color:#f87171;"" id=""lblTotalDebt"">0,00 ₺</div>
            </div>
        </div>

        <button onclick=""loadSummary()"" style=""background:#334155;color:#fff;border:none;padding:12px;border-radius:8px;font-weight:600;cursor:pointer;"">
            🔄 Bilgileri Şimdi Yenile
        </button>
    </div>

    <!-- 2. SEKME: BARKOD & STOK SORGULAMA -->
    <div id=""view-scan"" class=""view-section"">
        <div class=""camera-container"">
            <video id=""camVideo"" playsinline></video>
            <div class=""reticle""></div>
            <div class=""cam-controls"">
                <button class=""btn-ctrl"" id=""btnTorch"" onclick=""toggleTorch()"">💡 Fener</button>
                <button class=""btn-ctrl"" onclick=""switchCamera()"">🔄 Kamera</button>
            </div>
        </div>

        <div class=""search-bar"">
            <input type=""text"" id=""txtManual"" placeholder=""Barkod veya ürün adı yazın..."" onkeydown=""if(event.key==='Enter')searchManual()"">
            <button onclick=""searchManual()"">Ara</button>
        </div>

        <!-- Ürün Kartı -->
        <div id=""cardProduct"" class=""card"" style=""display:none;"">
            <div style=""display:flex;justify-content:space-between;align-items:flex-start;"">
                <div>
                    <div id=""lblProdName"" style=""font-size:1.15rem;font-weight:700;"">Ürün Adı</div>
                    <div id=""lblProdCode"" style=""font-size:0.8rem;color:#94a3b8;margin-top:2px;"">KOD</div>
                </div>
                <span id=""lblProdCat"" style=""background:#334155;padding:4px 8px;border-radius:6px;font-size:0.75rem;"">Kategori</span>
            </div>

            <div style=""margin-top:12px;padding:12px;background:#0f172a;border-radius:8px;display:flex;justify-content:space-between;align-items:center;"">
                <span style=""color:#94a3b8;font-size:0.85rem;"">Depo Stoku:</span>
                <span id=""lblCurrentStock"" style=""font-size:1.4rem;font-weight:800;color:#10b981;"">0 Adet</span>
            </div>

            <div class=""grid-2"" style=""margin-top:10px;"">
                <div class=""stat-mini"">
                    <div style=""color:#94a3b8;font-size:0.72rem;"">PERAKENDE SATIŞ</div>
                    <div class=""stat-mini-val"" style=""color:#38bdf8;"" id=""lblSalePrice"">0,00 ₺</div>
                </div>
                <div class=""stat-mini"">
                    <div style=""color:#94a3b8;font-size:0.72rem;"">TOPTAN FİYAT</div>
                    <div class=""stat-mini-val"" style=""color:#2dd4d4;"" id=""lblWsPrice"">0,00 ₺</div>
                </div>
            </div>

            <button onclick=""sendToDesktop()"" style=""margin-top:12px;background:#38bdf8;color:#0f172a;border:none;padding:12px;border-radius:8px;font-weight:700;width:100%;cursor:pointer;"">
                💻 Masaüstü Ekrana Aktar
            </button>
        </div>
    </div>

    <!-- 3. SEKME: BORÇLULAR -->
    <div id=""view-debtors"" class=""view-section"">
        <div class=""search-bar"">
            <input type=""text"" id=""txtDebtorSearch"" placeholder=""Müşteri adı veya telefon ara..."" oninput=""filterDebtors()"">
        </div>

        <div id=""debtorsList"" style=""display:flex;flex-direction:column;gap:10px;"">
            <div style=""text-align:center;padding:20px;color:#94a3b8;"">Borçlu müşteriler yükleniyor...</div>
        </div>
    </div>

    <!-- 4. SEKME: HIZLI TAHSİLAT -->
    <div id=""view-col"" class=""view-section"">
        <div class=""card"">
            <div class=""card-title"">💵 CEPTEN HIZLI TAHSİLAT GİRİŞİ</div>

            <div class=""form-group"">
                <label class=""form-label"">Müşteri Seçiniz (*):</label>
                <select id=""selAccount"" class=""form-select"">
                    <option value="""">Müşteriler Yükleniyor...</option>
                </select>
            </div>

            <div class=""form-group"">
                <label class=""form-label"">Tahsil Edilen Tutar (₺) (*):</label>
                <input type=""number"" id=""txtAmount"" class=""form-input"" placeholder=""0,00"" step=""0.01"">
            </div>

            <div class=""form-group"">
                <label class=""form-label"">Ödeme Türü:</label>
                <select id=""selMethod"" class=""form-select"">
                    <option value=""Nakit"">💵 Nakit</option>
                    <option value=""Kredi Kartı"">💳 Kredi Kartı / POS</option>
                    <option value=""Havale/EFT"">🏦 Havale / EFT / FAST</option>
                </select>
            </div>

            <div class=""form-group"">
                <label class=""form-label"">Açıklama / Not:</label>
                <input type=""text"" id=""txtNote"" class=""form-input"" placeholder=""Örn: Elden teslim alındı"">
            </div>

            <button class=""btn-primary"" onclick=""submitCollection()"">
                💰 TAHSİLATI SİSTEME KAYDET
            </button>
        </div>

        <div id=""colSuccessCard"" class=""card"" style=""display:none;background:#064e3b;border-color:#10b981;"">
            <div style=""font-size:1.1rem;font-weight:700;color:#34d399;"">✅ Tahsilat Kaydedildi!</div>
            <div id=""colSuccessText"" style=""margin-top:6px;font-size:0.9rem;""></div>
            <a id=""btnColWa"" href=""#"" target=""_blank"" class=""btn-wa-mini"" style=""margin-top:10px;display:inline-block;"">
                📲 Müşteriye WhatsApp Makbuzu Gönder
            </a>
        </div>
    </div>

    <!-- ALT MENÜ ÇUBUĞU -->
    <nav class=""bottom-nav"">
        <button class=""nav-item active"" onclick=""switchTab('kasa')"">
            <span class=""icon"">📊</span>
            <span>Kasa / Özet</span>
        </button>
        <button class=""nav-item"" onclick=""switchTab('scan')"">
            <span class=""icon"">📷</span>
            <span>Barkod / Stok</span>
        </button>
        <button class=""nav-item"" onclick=""switchTab('debtors')"">
            <span class=""icon"">👥</span>
            <span>Borçlular</span>
        </button>
        <button class=""nav-item"" onclick=""switchTab('col')"">
            <span class=""icon"">💵</span>
            <span>Tahsilat Al</span>
        </button>
    </nav>

    <script>
        // Bilgisayar uygulamasının verdiği bağlantıdaki erişim anahtarı (k) tüm API isteklerine otomatik eklenir
        const __k = new URLSearchParams(location.search).get('k') || '';
        const __origFetch = window.fetch.bind(window);
        window.fetch = (u, o) => __origFetch(u + (String(u).includes('?') ? '&' : '?') + 'k=' + encodeURIComponent(__k), o);
        let currentTab = 'kasa';
        let allDebtors = [];
        let codeReader = null;
        let isCameraRunning = false;
        let currentFacing = 'environment';
        let isTorchOn = false;
        let currentStream = null;
        let currentProductData = null;

        function showToast(msg) {
            const t = document.getElementById('toast');
            t.innerText = msg;
            t.style.display = 'block';
            setTimeout(() => t.style.display = 'none', 3000);
        }

        function switchTab(tab) {
            currentTab = tab;
            document.querySelectorAll('.view-section').forEach(el => el.classList.remove('active'));
            document.querySelectorAll('.nav-item').forEach(el => el.classList.remove('active'));

            const view = document.getElementById('view-' + tab);
            if (view) view.classList.add('active');

            const navIdx = ['kasa', 'scan', 'debtors', 'col'].indexOf(tab);
            if (navIdx >= 0) {
                document.querySelectorAll('.nav-item')[navIdx].classList.add('active');
            }

            if (tab === 'kasa') {
                loadSummary();
                stopCamera();
            } else if (tab === 'scan') {
                initCamera();
            } else if (tab === 'debtors') {
                stopCamera();
                loadDebtors();
            } else if (tab === 'col') {
                stopCamera();
                loadAccounts();
            }
        }

        // --- 1. KASA & ÖZET ---
        async function loadSummary() {
            try {
                const res = await fetch('/api/summary');
                const data = await res.json();
                if (data.success) {
                    document.getElementById('lblKasaTime').innerText = data.date;
                    document.getElementById('lblHeroCiro').innerText = formatMoney(data.totalSale);
                    document.getElementById('lblHeroSalesCount').innerText = data.saleCount;
                    document.getElementById('lblCashSale').innerText = formatMoney(data.cashSale);
                    document.getElementById('lblPosSale').innerText = formatMoney(data.posSale);
                    document.getElementById('lblCreditSale').innerText = formatMoney(data.creditSale);
                    document.getElementById('lblTotalCol').innerText = formatMoney(data.totalCol);
                    document.getElementById('lblNetCash').innerText = formatMoney(data.netCash);
                    document.getElementById('lblCritCount').innerText = data.critCount + ' Ürün';
                    document.getElementById('lblTotalDebt').innerText = formatMoney(data.totalDebt);
                }
            } catch(e) {
                console.error(e);
            }
        }

        function formatMoney(val) {
            return (val || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ₺';
        }

        // --- 2. BARKOD / KAMERA ---
        async function initCamera() {
            if (isCameraRunning) return;
            try {
                codeReader = new ZXing.BrowserMultiFormatReader();
                const videoEl = document.getElementById('camVideo');
                const stream = await navigator.mediaDevices.getUserMedia({
                    video: { facingMode: currentFacing, width: { ideal: 1280 }, height: { ideal: 720 } }
                });
                currentStream = stream;
                videoEl.srcObject = stream;
                await videoEl.play();
                isCameraRunning = true;

                codeReader.decodeFromVideoElement(videoEl, (result, err) => {
                    if (result) {
                        playBeep();
                        fetchProduct(result.getText());
                    }
                });
            } catch(e) {
                console.warn('Kamera açılamadı: ', e);
            }
        }

        function stopCamera() {
            if (codeReader) {
                codeReader.reset();
                codeReader = null;
            }
            if (currentStream) {
                currentStream.getTracks().forEach(t => t.stop());
                currentStream = null;
            }
            isCameraRunning = false;
        }

        function playBeep() {
            try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.value = 1800;
                gain.gain.value = 0.2;
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start();
                setTimeout(() => { osc.stop(); ctx.close(); }, 80);
            } catch(e) {}
        }

        async function fetchProduct(query) {
            try {
                const res = await fetch('/api/product?q=' + encodeURIComponent(query));
                const data = await res.json();
                if (data.success && data.product) {
                    renderProduct(data.product);
                } else {
                    alert('Ürün bulunamadı: ' + (data.message || query));
                }
            } catch(e) {
                alert('Sorgu hatası: ' + e.message);
            }
        }

        function renderProduct(p) {
            currentProductData = p;
            document.getElementById('lblProdName').innerText = p.name;
            document.getElementById('lblProdCode').innerText = 'Kod: ' + p.code + (p.barcode ? ' • Barkod: ' + p.barcode : '');
            document.getElementById('lblProdCat').innerText = p.category;
            document.getElementById('lblCurrentStock').innerText = p.currentStock + ' ' + p.unit;
            document.getElementById('lblSalePrice').innerText = formatMoney(p.salePrice);
            document.getElementById('lblWsPrice').innerText = formatMoney(p.wholesalePrice);
            document.getElementById('cardProduct').style.display = 'block';
        }

        async function sendToDesktop() {
            if (!currentProductData) return;
            try {
                const res = await fetch('/api/send-desktop?barcode=' + encodeURIComponent(currentProductData.barcode || currentProductData.code));
                const d = await res.json();
                if (d.success) showToast('💻 Masaüstüne aktarıldı!');
            } catch(e) {
                alert(e.message);
            }
        }

        function searchManual() {
            const v = document.getElementById('txtManual').value.trim();
            if (v) fetchProduct(v);
        }

        async function toggleTorch() {
            if (!currentStream) return;
            const track = currentStream.getVideoTracks()[0];
            if (track && track.getCapabilities && track.getCapabilities().torch) {
                isTorchOn = !isTorchOn;
                await track.applyConstraints({ advanced: [{ torch: isTorchOn }] });
                document.getElementById('btnTorch').innerText = isTorchOn ? '🔦 Açık' : '💡 Fener';
            } else {
                alert('Fener desteklenmiyor');
            }
        }

        function switchCamera() {
            currentFacing = currentFacing === 'environment' ? 'user' : 'environment';
            stopCamera();
            initCamera();
        }

        // --- 3. BORÇLULAR ---
        async function loadDebtors() {
            const listEl = document.getElementById('debtorsList');
            listEl.innerHTML = '<div style=""text-align:center;padding:20px;color:#94a3b8;"">Yükleniyor...</div>';
            try {
                const res = await fetch('/api/debtors');
                const data = await res.json();
                if (data.success) {
                    allDebtors = data.debtors;
                    renderDebtors(allDebtors);
                }
            } catch(e) {
                listEl.innerHTML = '<div style=""color:#ef4444;"">Yüklenemedi: ' + e.message + '</div>';
            }
        }

        function renderDebtors(list) {
            const listEl = document.getElementById('debtorsList');
            if (!list || list.length === 0) {
                listEl.innerHTML = '<div style=""text-align:center;padding:20px;color:#94a3b8;"">Borçlu müşteri bulunamadı.</div>';
                return;
            }

            let html = '';
            list.forEach(d => {
                const msg = encodeURIComponent('Sayın ' + d.name + ',\nBilensis cari hesabınızda güncel borç bakiyeniz ' + formatMoney(d.balance) + '\'dir. Bilginize sunar, iyi günler dileriz.');
                const waLink = d.phone ? 'https://wa.me/90' + d.phone.replace(/[^0-9]/g, '').replace(/^0/, '') + '?text=' + msg : '#';

                html += `
                <div class=""debtor-item"">
                    <div>
                        <div class=""debtor-name"">${d.name}</div>
                        <div class=""debtor-phone"">${d.phone || 'Tel yok'}</div>
                        ${d.phone ? `<a href=""${waLink}"" target=""_blank"" class=""btn-wa-mini"">📲 WhatsApp Hatırlat</a>` : ''}
                    </div>
                    <div>
                        <div class=""debtor-bal"">${formatMoney(d.balance)}</div>
                    </div>
                </div>`;
            });
            listEl.innerHTML = html;
        }

        function filterDebtors() {
            const q = document.getElementById('txtDebtorSearch').value.toLowerCase().trim();
            const filtered = allDebtors.filter(d => d.name.toLowerCase().includes(q) || (d.phone && d.phone.includes(q)));
            renderDebtors(filtered);
        }

        // --- 4. HIZLI TAHSİLAT ---
        async function loadAccounts() {
            const sel = document.getElementById('selAccount');
            try {
                const res = await fetch('/api/accounts');
                const data = await res.json();
                if (data.success) {
                    let opts = '<option value="""">-- Müşteri Seçiniz --</option>';
                    data.accounts.forEach(a => {
                        const balText = a.balance > 0 ? ' (Borç: ' + formatMoney(a.balance) + ')' : '';
                        opts += `<option value=""${a.id}"" data-phone=""${a.phone||''}"">${a.name}${balText}</option>`;
                    });
                    sel.innerHTML = opts;
                }
            } catch(e) {
                console.error(e);
            }
        }

        async function submitCollection() {
            const sel = document.getElementById('selAccount');
            const accId = sel.value;
            const amt = parseFloat(document.getElementById('txtAmount').value);
            const mth = document.getElementById('selMethod').value;
            const note = document.getElementById('txtNote').value.trim();

            if (!accId || isNaN(amt) || amt <= 0) {
                alert('Lütfen bir müşteri seçiniz ve geçerli bir tutar giriniz.');
                return;
            }

            try {
                const res = await fetch('/api/add-collection', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ accountId: parseInt(accId), amount: amt, method: mth, note: note })
                });
                const data = await res.json();
                if (data.success) {
                    showToast('✅ ' + data.message);
                    document.getElementById('txtAmount').value = '';
                    document.getElementById('txtNote').value = '';

                    const succCard = document.getElementById('colSuccessCard');
                    document.getElementById('colSuccessText').innerText = data.customerName + ' carisinden ' + formatMoney(data.amount) + ' alındı. Kalan Borç: ' + formatMoney(data.newBalance);
                    
                    if (data.customerPhone) {
                        const recMsg = encodeURIComponent('Sayın ' + data.customerName + ',\n' + formatMoney(data.amount) + ' tutarındaki ödemeniz ' + mth + ' olarak tahsil edilmiştir.\nGüncel kalan bakiyeniz: ' + formatMoney(data.newBalance) + '\nTeşekkür ederiz.');
                        const btnWa = document.getElementById('btnColWa');
                        btnWa.href = 'https://wa.me/90' + data.customerPhone.replace(/[^0-9]/g, '').replace(/^0/, '') + '?text=' + recMsg;
                        btnWa.style.display = 'inline-block';
                    } else {
                        document.getElementById('btnColWa').style.display = 'none';
                    }

                    succCard.style.display = 'block';
                    loadAccounts();
                } else {
                    alert('Hata: ' + data.message);
                }
            } catch(e) {
                alert('Bağlantı hatası: ' + e.message);
            }
        }

        window.addEventListener('load', () => {
            loadSummary();
        });
    </script>
</body>
</html>";
    }
}
