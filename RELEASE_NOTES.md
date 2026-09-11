# مركز تحكم Roblox — الإصدار 1.0.0

تطبيق ويندوز عربي بسيط لإدارة خدمتي **ByeDPI** و**ProxiFyre** وتشغيل
**Roblox** عبر بروكسي SOCKS5 محلي (`127.0.0.1:1080`).

> البروكسي محلي فقط، والتوجيه مخصص لعمليات Roblox — بقية برامجك لا تمر عبره.

---

## الملفات المرفقة

| الملف | الوصف |
| --- | --- |
| `ByeDPI-Control-1.0.0-win-x64.zip` | **النسخة الموصى بها** — التطبيق + كل سكربتات التثبيت + التوثيق. تحتاج [.NET Desktop Runtime 9](https://dotnet.microsoft.com/download/dotnet/9.0). |
| `ByeDPIControl-1.0.0-win-x64-selfcontained.zip` | التطبيق فقط في ملف واحد مستقل، لا يحتاج تثبيت .NET إطلاقًا. |
| `SHA256SUMS.txt` | بصمات SHA-256 للتحقق من سلامة الملفات. |

---

## التنزيل والتثبيت

### 1) نزّل وفك الضغط

نزّل `ByeDPI-Control-1.0.0-win-x64.zip` وفك الضغط في مجلد مثل
`C:\Tools\ByeDPI Control`.

### 2) افتح PowerShell كمسؤول

اضغط بزر الفأرة الأيمن على **PowerShell** ← **تشغيل كمسؤول**، ثم انتقل للمجلد:

```powershell
cd 'C:\Tools\ByeDPI Control'
```

إذا ظهرت رسالة `running scripts is disabled`، نفّذ أولًا:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

### 3) نزّل الأدوات المطلوبة

```powershell
.\scripts\Get-Dependencies.ps1
```

يُنزّل `ciadpi.exe` (ByeDPI) وProxiFyre من مستودعيهما الرسميين.

### 4) ثبّت الخدمات

```powershell
.\scripts\Install-RobloxServices.ps1
```

### 5) اضبط الجدار الناري

```powershell
.\scripts\Configure-Firewall.ps1
```

### 6) أنشئ اختصار سطح المكتب (اختياري)

```powershell
.\scripts\New-DesktopShortcut.ps1
```

### 7) شغّل التطبيق

شغّل `ByeDPIControl.exe` أو اختصار **«تحكم Roblox»**، ثم اضغط **▶ تشغيل الكل**.

---

## المتطلبات

- ويندوز 10 أو 11 (x64)
- صلاحية مسؤول لتثبيت وتشغيل الخدمات
- [.NET Desktop Runtime 9](https://dotnet.microsoft.com/download/dotnet/9.0) — غير مطلوب مع النسخة المستقلة

---

## التحقق من الملفات

```powershell
Get-FileHash .\ByeDPI-Control-1.0.0-win-x64.zip -Algorithm SHA256
```

قارن الناتج بما في `SHA256SUMS.txt`.

---

## المحتويات

- `ByeDPIControl.exe` — التطبيق (WinForms، .NET 9)
- `scripts\Get-Dependencies.ps1` — تنزيل ByeDPI وProxiFyre
- `scripts\Install-RobloxServices.ps1` — تثبيت وضبط الخدمتين
- `scripts\Configure-Firewall.ps1` — قاعدة الجدار الناري
- `scripts\New-DesktopShortcut.ps1` — اختصار سطح المكتب
- `scripts\Uninstall-RobloxServices.ps1` — إزالة كاملة
- `scripts\Build.ps1` — بناء من الكود المصدري
- `README.md` — التوثيق الكامل (عربي + إنجليزي)
- `اقرأني.txt` — تعليمات مختصرة
- `LICENSE` — رخصة MIT

---

## إلغاء التثبيت

```powershell
.\scripts\Uninstall-RobloxServices.ps1
```

---

## إخلاء المسؤولية

هذا الإصدار يحتوي على طبقة التحكم فقط. **ByeDPI** و**ProxiFyre** مشروعان
خارجيان ولا يُضمَّنان هنا، ويخضعان لرخصتيهما:

- [hufrea/byedpi](https://github.com/hufrea/byedpi) — MIT
- [wiresock/proxifyre](https://github.com/wiresock/proxifyre) — LGPL-2.1

استخدم المشروع بمسؤوليتك الشخصية ووفق القوانين المعمول بها في بلدك.
