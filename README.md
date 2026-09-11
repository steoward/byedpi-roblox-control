<div dir="rtl">

# مركز تحكم Roblox — ByeDPI + ProxiFyre

واجهة رسومية عربية بسيطة لإدارة خدمتي **ByeDPI** و**ProxiFyre** على ويندوز،
مخصّصة لتشغيل **Roblox** عبر بروكسي SOCKS5 محلي مع تجاوز حجب مزوّد الخدمة.

> **كل شيء يبقى على جهازك.** البروكسي يستمع على `127.0.0.1:1080` فقط،
> والتوجيه محصور بعمليات Roblox — بقية برامجك لا تمر عبر البروكسي.

---

## المحتويات

- [ما هو هذا المشروع؟](#ما-هو-هذا-المشروع)
- [المتطلبات](#المتطلبات)
- [التنزيل والتثبيت السريع](#التنزيل-والتثبيت-السريع)
- [التركيب خطوة بخطوة](#التركيب-خطوة-بخطوة)
- [استخدام التطبيق](#استخدام-التطبيق)
- [البناء من الكود المصدري](#البناء-من-الكود-المصدري)
- [البنية التفصيلية للملفات](#البنية-التفصيلية-للملفات)
- [حل المشكلات](#حل-المشكلات)
- [إلغاء التثبيت](#إلغاء-التثبيت)
- [الأسئلة الشائعة](#الأسئلة-الشائعة)
- [إخلاء المسؤولية والترخيص](#إخلاء-المسؤولية-والترخيص)

---

## ما هو هذا المشروع؟

يوفّر المشروع طبقتين:

| الطبقة | الوصف |
| --- | --- |
| **التطبيق** (`ByeDPIControl.exe`) | واجهة WinForms عربية تعرض حالة الخدمتين، وتشغّلها/توقفها/تعيد تشغيلها، وتفتح Roblox والسجلات. |
| **سكربتات PowerShell** | تُثبّت الخدمتين وتضبط الجدار الناري وتنشئ الاختصار — أي أنها تؤتمت الإعداد الكامل. |

التطبيق نفسه **لا يعدّل أي إعداد**؛ هو فقط يتحكم بالخدمات عبر أوامر ويندوز
الرسمية (`StartService` / `ControlService`) ويطلب صلاحية المسؤول عند الحاجة فقط.

### كيف يعمل؟

```
Roblox  ──►  ProxiFyre (يوجّه عمليات Roblox فقط)
                  │
                  ▼
          127.0.0.1:1080  (SOCKS5 محلي)
                  │
                  ▼
          ByeDPI / ciadpi (تجزئة حزم DPI)
                  │
                  ▼
             الإنترنت
```

---

## المتطلبات

| المتطلب | التفاصيل |
| --- | --- |
| نظام التشغيل | ويندوز 10 أو 11 (x64) |
| الصلاحيات | صلاحية مسؤول لتثبيت/تشغيل الخدمات |
| .NET | [.NET Desktop Runtime 9](https://dotnet.microsoft.com/download/dotnet/9.0) — **غير مطلوب** إذا نزّلت النسخة `self-contained` |
| ByeDPI | ملف `ciadpi.exe` من [hufrea/byedpi](https://github.com/hufrea/byedpi/releases) |
| ProxiFyre | من [wiresock/proxifyre](https://github.com/wiresock/proxifyre/releases) |

> الأدوات الخارجية **لا تُضمَّن** في هذا المستودع. استخدم
> `scripts\Get-Dependencies.ps1` لتنزيلها تلقائيًا من مصادرها الرسمية.

---

## التنزيل والتثبيت السريع

### الطريقة الأولى: نسخة جاهزة (موصى بها)

1. افتح صفحة **[Releases](../../releases/latest)**.
2. نزّل `ByeDPI-Control-1.0.0-win-x64.zip`.
3. فك الضغط في أي مجلد (مثلًا `C:\Tools\ByeDPI Control`).
4. شغّل `Install-RobloxServices.ps1` **كمسؤول** (انظر [التركيب خطوة بخطوة](#التركيب-خطوة-بخطوة)).
5. شغّل `ByeDPIControl.exe`.

### الطريقة الثانية: استنساخ المستودع

```powershell
git clone https://github.com/steoward/byedpi-roblox-control.git
cd byedpi-roblox-control
```

ثم اتبع الخطوات أدناه.

### الطريقة الثالثة: أمر واحد (PowerShell)

```powershell
irm https://raw.githubusercontent.com/steoward/byedpi-roblox-control/main/scripts/Get-Dependencies.ps1 -OutFile Get-Dependencies.ps1
.\Get-Dependencies.ps1
```

---

## التركيب خطوة بخطوة

> كل الأوامر التالية تُنفَّذ في **PowerShell كمسؤول**
> (اضغط بزر الفأرة الأيمن على PowerShell ← «تشغيل كمسؤول»).

### 0) السماح بتشغيل السكربتات (مرة واحدة فقط)

إذا ظهرت رسالة `running scripts is disabled`، نفّذ:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

هذا يسمح للسكربتات بالعمل في هذه النافذة فقط، ثم يعود كل شيء لطبيعته.

### 1) تنزيل الأدوات المطلوبة

```powershell
.\scripts\Get-Dependencies.ps1
```

يُنزّل:
- `ciadpi.exe` ← `%USERPROFILE%\.local\bin\`
- ProxiFyre ← `%LOCALAPPDATA%\Programs\ProxiFyre\`

إن كانت الأدوات مثبّتة مسبقًا فسيتم تخطّيها. لإعادة التنزيل أضف `-Force`.

### 2) تثبيت الخدمات

```powershell
.\scripts\Install-RobloxServices.ps1
```

يقوم بـ:
1. تسجيل خدمة **ByeDPI** (تشغيل تلقائي) مع وسائط تجاوز DPI المناسبة.
2. كتابة `app-config.json` لـ ProxiFyre لتوجيه Roblox فقط.
3. تسجيل خدمة **ProxiFyreService** عبر أمر `install` الرسمي.
4. ضبط إعادة التشغيل التلقائي عند الفشل للخدمتين.
5. تشغيل الخدمتين والتحقق من المنفذ `127.0.0.1:1080`.

في النهاية يُنشئ ملف `install-result.json` ويطبع ملخّصًا:

```json
{
  "ByeDPIService":    { "State": "Running", "StartMode": "Auto" },
  "ProxiFyreService": { "State": "Running", "StartMode": "Auto" },
  "SocksListener":    [ "127.0.0.1:1080" ]
}
```

### 3) ضبط الجدار الناري

```powershell
.\scripts\Configure-Firewall.ps1
```

يضيف قاعدة تسمح لـ ProxiFyre بإعادة توجيه حركة Roblox، ثم يعيد تشغيل الخدمة.

### 4) إنشاء اختصار على سطح المكتب (اختياري)

```powershell
.\scripts\New-DesktopShortcut.ps1
```

يُنشئ اختصارًا باسم **«تحكم Roblox»** على سطح المكتب.

---

## استخدام التطبيق

شغّل **`ByeDPIControl.exe`** أو اختصار **«تحكم Roblox»**.

### الأزرار

| الزر | الوظيفة |
| --- | --- |
| **▶ تشغيل الكل** | يشغّل خدمتي ByeDPI وProxiFyre. |
| **■ إيقاف الكل** | يوقف الخدمتين. |
| **↻ إعادة التشغيل** | يعيد تشغيلهما — الحل الأول عند مشاكل الاتصال. |
| **تشغيل Roblox** | يفتح اللعبة مباشرة. |
| **فتح السجلات** | يفتح مجلد تشخيص ProxiFyre. |
| **تحديث** | يحدّث الحالة فورًا. |

### الحالة

- **✓ الاتصال جاهز ويعمل** — الخدمتان تعملان والمنفذ 1080 يستمع.
- **○ الاتصال متوقف** — اضغط «تشغيل الكل».
- **! يحتاج إلى انتباه** — إحدى الخدمات غير جاهزة؛ جرّب «إعادة التشغيل».

> سيطلب ويندوز صلاحية المسؤول **فقط** عند تشغيل الخدمات أو إيقافها أو
> إعادة تشغيلها. تحديث الحالة لا يحتاج أي صلاحيات.

---

## البناء من الكود المصدري

### المتطلبات

- [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) أو أحدث
- ويندوز (المشروع يستهدف `net9.0-windows` مع WinForms)

### بالأمر المباشر

```powershell
git clone https://github.com/steoward/byedpi-roblox-control.git
cd byedpi-roblox-control

# نسخة عادية (تحتاج .NET Desktop Runtime 9 على جهاز المستخدم)
dotnet publish .\Source\ByeDPIControl.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# نسخة مستقلة (لا تحتاج تثبيت .NET إطلاقًا)
dotnet publish .\Source\ByeDPIControl.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

الناتج في:
`Source\bin\Release\net9.0-windows\win-x64\publish\ByeDPIControl.exe`

### عبر سكربت البناء

```powershell
.\scripts\Build.ps1                  # نسخة عادية
.\scripts\Build.ps1 -SelfContained   # نسخة مستقلة
```

ينسخ الناتج إلى `dist\ByeDPIControl.exe`.

---

## البنية التفصيلية للملفات

```
byedpi-roblox-control/
├── ByeDPIControl.exe              # نسخة جاهزة للتشغيل
├── اقرأني.txt                      # تعليمات مختصرة بالعربية
├── LICENSE                        # رخصة MIT
├── README.md                      # هذا الملف
├── Source/
│   ├── ByeDPIControl.csproj       # تعريف مشروع .NET 9 WinForms
│   ├── Program.cs                 # كامل كود التطبيق (الواجهة + التحكم بالخدمات)
│   └── app.ico                    # أيقونة التطبيق
└── scripts/
    ├── Get-Dependencies.ps1       # تنزيل ByeDPI وProxiFyre من مصادرهما الرسمية
    ├── Install-RobloxServices.ps1 # تثبيت وضبط الخدمتين
    ├── Configure-Firewall.ps1     # قاعدة الجدار الناري
    ├── New-DesktopShortcut.ps1    # اختصار سطح المكتب
    ├── Uninstall-RobloxServices.ps1 # إزالة الخدمات والقاعدة
    └── Build.ps1                  # بناء التطبيق
```

### وسائط تشغيل ByeDPI المستخدمة

```text
--ip 127.0.0.1 --split 1 --disorder 3+s --mod-http=h,d --auto=torst --tlsrec 1+s
```

| الوسيط | المعنى |
| --- | --- |
| `--ip 127.0.0.1` | الاستماع على الجهاز المحلي فقط |
| `--split 1` | تقسيم حزمة TCP الأولى (تجاوز DPI) |
| `--disorder 3+s` | إرسال الحزم بترتيب معكوس |
| `--mod-http=h,d` | تعديل ترويسات HTTP |
| `--auto=torst` | وضع تلقائي متوافق مع TLS |
| `--tlsrec 1+s` | تقسيم تسجيل TLS |

لتغييرها استخدم `-ByeDpiArguments` عند التثبيت:

```powershell
.\scripts\Install-RobloxServices.ps1 -ByeDpiArguments '--ip 127.0.0.1 --split 1 --disorder 3+s'
```

### إعداد توجيه ProxiFyre

يُوجَّه فقط ما يطابق `appNames` في `app-config.json`:

```json
{
  "logLevel": "Warning",
  "bypassLan": true,
  "proxies": [
    {
      "appNames": [
        "RobloxPlayerBeta",
        "C:\\Program Files\\WindowsApps\\ROBLOXCorporation.RobloxGDK"
      ],
      "socks5ProxyEndpoint": "127.0.0.1:1080",
      "socks5Transport": "TCP",
      "supportedProtocols": [ "TCP", "UDP" ],
      "supportedAddressFamilies": [ "IPv4", "IPv6" ]
    }
  ],
  "excludes": []
}
```

---

## حل المشكلات

### البروكسي لا يعمل بعد التثبيت

```powershell
Get-Service ByeDPI, ProxiFyreService | Format-Table Name, Status, StartType
Get-NetTCPConnection -LocalPort 1080 -State Listen
```

إن كان المنفذ `1080` غير موجود، أعد التشغيل من التطبيق أو:

```powershell
Restart-Service ByeDPI -Force
Restart-Service ProxiFyreService -Force
```

### «running scripts is disabled on this system»

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

### نافذة صلاحيات المسؤول لا تظهر

تأكد أنك شغّلت التطبيق من مجلد محلي (لا من قرص شبكي أو مجلد مضغوط مباشرة).
جرّب فك الضغط كاملًا قبل التشغيل.

### Roblox يعمل لكن بلا إنترنت

1. اضغط **إعادة التشغيل** في التطبيق.
2. تأكد أن `ciadpi.exe` ما زال موجودًا في مساره:
   ```powershell
   Test-Path "$env:USERPROFILE\.local\bin\ciadpi.exe"
   ```
3. راجع سجلات ProxiFyre:
   ```powershell
   Get-Content "$env:LOCALAPPDATA\Programs\ProxiFyre\logs\*.log" -Tail 50
   ```

### «تعذر فتح خدمة ByeDPI»

الخدمة غير مثبّتة. نفّذ:

```powershell
.\scripts\Install-RobloxServices.ps1
```

### تغيّر مسار ciadpi.exe

أعد التثبيت مع تحديد المسار الصحيح:

```powershell
.\scripts\Install-RobloxServices.ps1 -ByeDpiExe 'D:\tools\ciadpi.exe'
```

---

## إلغاء التثبيت

**كمسؤول:**

```powershell
.\scripts\Uninstall-RobloxServices.ps1
```

يوقف ويحذف الخدمتين ويزيل قاعدة الجدار الناري.
لحذف ملفات الإعداد المُنشأة أيضًا أضف `-RemoveConfig`:

```powershell
.\scripts\Uninstall-RobloxServices.ps1 -RemoveConfig
```

ثم احذف مجلد المشروع والاختصار يدويًا إن أردت.

---

## الأسئلة الشائعة

**هل يمرّ كل جهازي عبر البروكسي؟**
لا. `app-config.json` يوجّه عمليات Roblox فقط، والبروكسي يستمع على `127.0.0.1` فقط.

**هل يرسل التطبيق أي بيانات لأي جهة؟**
لا. لا يتصل بالإنترنت إطلاقًا؛ يقرأ حالة الخدمات المحلية فقط.

**هل يعمل مع نسخة Roblox من Microsoft Store؟**
نعم، ومسار `ROBLOXCorporation.RobloxGDK` مُضمَّن في الإعداد افتراضيًا.

**هل أحتاج إعادة التثبيت بعد كل تحديث لويندوز؟**
عادةً لا. إن تعطّلت الخدمة بعد تحديث كبير، شغّل `Install-RobloxServices.ps1` مجددًا.

**ما الفرق بين النسخة العادية والمستقلة؟**
العادية أصغر لكنها تحتاج تثبيت .NET Desktop Runtime 9. المستقلة تعمل مباشرة لكن حجمها أكبر.

---

## إخلاء المسؤولية والترخيص

هذا المشروع أداة **تحكم** فقط؛ لا يحتوي على ByeDPI ولا ProxiFyre ولا أي جزء منهما.
الأدوات الخارجية مملوكة لمطوّريها وتخضع لرخصها الخاصة:

- [hufrea/byedpi](https://github.com/hufrea/byedpi) — رخصة MIT
- [wiresock/proxifyre](https://github.com/wiresock/proxifyre) — رخصة LGPL-2.1

استخدم المشروع بمسؤوليتك الشخصية ووفق القوانين المعمول بها في بلدك.

كود هذا المستودع مُرخَّص تحت **MIT** — راجع [LICENSE](LICENSE).

</div>

---

<div dir="ltr">

# Roblox Control Center — ByeDPI + ProxiFyre

A small Arabic-first Windows GUI for managing **ByeDPI** and **ProxiFyre**,
routing **Roblox** through a local SOCKS5 proxy to bypass ISP DPI blocking.

> **Everything stays on your machine.** The proxy listens on `127.0.0.1:1080`
> only, and redirection is limited to Roblox processes — your other
> applications never go through the proxy.

---

## Download & Quick Install

### Option 1: Prebuilt release (recommended)

1. Open the **[Releases](../../releases/latest)** page.
2. Download `ByeDPI-Control-1.0.0-win-x64.zip`.
3. Extract it anywhere (e.g. `C:\Tools\ByeDPI Control`).
4. Run `Install-RobloxServices.ps1` **as Administrator**.
5. Run `ByeDPIControl.exe`.

### Option 2: Clone and build

```powershell
git clone https://github.com/steoward/byedpi-roblox-control.git
cd byedpi-roblox-control

# Fetch third-party dependencies (ByeDPI + ProxiFyre)
.\scripts\Get-Dependencies.ps1

# Install and configure both services (Administrator)
.\scripts\Install-RobloxServices.ps1

# Optional: firewall rule + desktop shortcut
.\scripts\Configure-Firewall.ps1
.\scripts\New-DesktopShortcut.ps1
```

> All commands above run in **PowerShell as Administrator**.
> If scripts are blocked, first run:
> `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`

---

## Requirements

| Requirement | Details |
| --- | --- |
| OS | Windows 10 or 11 (x64) |
| Privileges | Administrator, to install/control the services |
| .NET | [.NET Desktop Runtime 9](https://dotnet.microsoft.com/download/dotnet/9.0) — not needed for `self-contained` builds |
| ByeDPI | `ciadpi.exe` from [hufrea/byedpi](https://github.com/hufrea/byedpi/releases) |
| ProxiFyre | from [wiresock/proxifyre](https://github.com/wiresock/proxifyre/releases) |

---

## Application buttons

| Button | Action |
| --- | --- |
| **▶ Start All** | Starts ByeDPI and ProxiFyre. |
| **■ Stop All** | Stops both services. |
| **↻ Restart** | Restarts both — first remedy for connection problems. |
| **Launch Roblox** | Opens the game directly. |
| **Open Logs** | Opens the ProxiFyre diagnostics folder. |
| **Refresh** | Updates the status immediately. |

Administrator rights are requested **only** when starting, stopping, or
restarting services. Status polling requires no elevation.

---

## Building from source

```powershell
# Framework-dependent (needs .NET Desktop Runtime 9)
dotnet publish .\Source\ByeDPIControl.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# Self-contained (no runtime installation required)
dotnet publish .\Source\ByeDPIControl.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or use the helper script:

```powershell
.\scripts\Build.ps1                  # framework-dependent
.\scripts\Build.ps1 -SelfContained   # self-contained
```

Output: `Source\bin\Release\net9.0-windows\win-x64\publish\ByeDPIControl.exe`

---

## Repository layout

```
byedpi-roblox-control/
├── ByeDPIControl.exe              # Ready-to-run build
├── اقرأني.txt                      # Short Arabic instructions
├── LICENSE                        # MIT
├── README.md
├── Source/
│   ├── ByeDPIControl.csproj       # .NET 9 WinForms project
│   ├── Program.cs                 # Full application source
│   └── app.ico
└── scripts/
    ├── Get-Dependencies.ps1       # Downloads ByeDPI + ProxiFyre from upstream
    ├── Install-RobloxServices.ps1 # Installs and configures both services
    ├── Configure-Firewall.ps1     # Adds the firewall rule
    ├── New-DesktopShortcut.ps1    # Creates a desktop shortcut
    ├── Uninstall-RobloxServices.ps1
    └── Build.ps1
```

---

## Troubleshooting

### Proxy is not listening

```powershell
Get-Service ByeDPI, ProxiFyreService | Format-Table Name, Status, StartType
Get-NetTCPConnection -LocalPort 1080 -State Listen
Restart-Service ByeDPI -Force
Restart-Service ProxiFyreService -Force
```

### Scripts are disabled

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

### Roblox runs but has no internet

1. Press **Restart** in the app.
2. Verify the binary still exists:
   `Test-Path "$env:USERPROFILE\.local\bin\ciadpi.exe"`
3. Check ProxiFyre logs:
   `Get-Content "$env:LOCALAPPDATA\Programs\ProxiFyre\logs\*.log" -Tail 50`

---

## Uninstall

```powershell
# As Administrator
.\scripts\Uninstall-RobloxServices.ps1
.\scripts\Uninstall-RobloxServices.ps1 -RemoveConfig   # also delete generated configs
```

---

## License & disclaimer

This project contains **only** the control layer. ByeDPI and ProxiFyre are
third-party projects, not bundled here, and remain under their own licenses:

- [hufrea/byedpi](https://github.com/hufrea/byedpi) — MIT
- [wiresock/proxifyre](https://github.com/wiresock/proxifyre) — LGPL-2.1

Use at your own risk and in compliance with the laws of your country.

The code in this repository is licensed under **MIT** — see [LICENSE](LICENSE).

</div>
