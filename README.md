# ChibiPulse

**A precision tiny-timer HUD that presses SPACE for you at the exact moment you choose.**
**عدّاد دقيق مع شاشة عائمة، يضغط المسافة نيابةً عنك في اللحظة اللي تحددها.**

[**⬇ Download the latest release**](https://github.com/0Sraj/ChibiPulse/releases/latest) · [**⬇ تنزيل آخر إصدار**](https://github.com/0Sraj/ChibiPulse/releases/latest)

---

## English

Press your trigger key in-game. A countdown starts on a floating HUD drawn over the game, and at the
moment you configured, ChibiPulse injects a **SPACE** keystroke for you.

Single-file WinForms app. No installer, no external packages, no dependencies beyond what Windows
already ships.

### How it works

1. You press the trigger key (default `E`) anywhere.
2. A phase begins — the HUD counts down from the duration you set (default `4.00s`).
3. At the jump moment (default `3.90s` into the phase) a SPACE keystroke is injected.
4. Shortly before the phase ends you get a warning cue, then the app returns to `READY`.

### Requirements

- Windows 10 or newer
- .NET Framework 4.8 — already installed on Windows 10 version 1903 and later

### Defaults

| Setting | Default |
|---|---|
| Trigger | `E` |
| Toggle listening | `F8` |
| Tiny duration | `4.00s` |
| Jump at | `3.90s` (measured from the **start** of the phase) |
| Warn before | `0.20s` |
| Cooldown | `0.00s` |

Over 100 key choices per role: letters, digits, F1–F12, numpad, side mouse buttons and punctuation.

Settings are written to `config.ini` next to the executable. **Delete that file to restore the defaults.**

### Known issues

Please read before using:

- **Do not bind `Space` as the trigger while auto-jump is on.** The SPACE the app injects is read back as a
  fresh trigger press, starting another phase — a loop that fires a SPACE into your focused window every
  few seconds.
- **Pausing with `F8` does not stop a phase that already started.** It runs to completion and still injects
  SPACE, even though the interface reads `PAUSED`.
- **The Test run button ignores the paused state** and injects a real SPACE.
- **Warn before is silent unless Beep is enabled.** The voice option never announces the warning.
- **There is no game-window filter.** The trigger fires from any application, and the SPACE goes to whatever
  window has focus at the time. Press `F8` before typing in chat or a browser.
- **Changing the duration does not move the jump moment.** Re-check it after any duration change.
- The app uses a noticeable slice of a CPU core even while idle.
- Some games ignore keystrokes injected through `SendInput`, and some anti-cheat systems detect them.
  Make sure tools like this are permitted in the game you play.

### Building from source

This repository holds `ChibiPulse.cs` only. To build it you also need an `app.manifest` declaring
`PerMonitorV2` DPI awareness and an `asInvoker` execution level, and you must pass `/codepage:65001` to the
compiler — the source is UTF-8 **without a BOM** and contains Arabic string literals, so without that flag
`csc.exe` decodes them using the system ANSI codepage and bakes mojibake into the executable.

```bat
csc /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
    /win32manifest:app.manifest ^
    /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll ^
    /out:ChibiPulse.exe ChibiPulse.cs
```

The compiler ships with Windows at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, so no Visual
Studio install is required.

### License

MIT

---

<div dir="rtl">

## العربية

اضغط زر التحول داخل اللعبة. يبدأ العد التنازلي على شاشة عائمة فوق اللعبة، وعند اللحظة اللي ضبطتها يرسل
ChibiPulse ضغطة **مسافة** تلقائياً نيابةً عنك.

البرنامج ملف مصدر واحد بواجهة WinForms. بدون تنصيب، وبدون أي حزم خارجية، وما يحتاج شيء غير الموجود أصلاً
في ويندوز.

### كيف يشتغل

1. تضغط زر التحول (افتراضياً `E`) من أي مكان.
2. يبدأ الطور — الشاشة العائمة تعدّ تنازلياً من المدة اللي حددتها (افتراضياً `4.00` ثانية).
3. عند لحظة القفزة (افتراضياً `3.90` ثانية من بداية الطور) تُرسل ضغطة مسافة.
4. قبل نهاية الطور بقليل يوصلك تنبيه، ثم يرجع البرنامج لحالة `READY`.

### المتطلبات

- ويندوز 10 أو أحدث
- .NET Framework 4.8 — مثبّت أصلاً مع ويندوز 10 إصدار 1903 فما فوق

### الإعدادات الافتراضية

| الإعداد | الافتراضي |
|---|---|
| زر التحول | `E` |
| تشغيل / إيقاف الاستشعار | `F8` |
| مدة التحول | `4.00` ثانية |
| وقت القفزة | `3.90` ثانية (محسوبة من **بداية** الطور) |
| تحذير مبكر | `0.20` ثانية |
| كول داون | `0.00` ثانية |

أكثر من 100 خيار زر لكل دور: حروف، أرقام، F1–F12، لوحة الأرقام، أزرار الماوس الجانبية، والرموز.

الإعدادات تُحفظ في ملف `config.ini` بجانب البرنامج. **احذف هذا الملف لاسترجاع الإعدادات الافتراضية.**

### مشاكل معروفة

اقرأها قبل الاستخدام:

- **لا تختر `Space` كزر تحول مع تفعيل القفزة التلقائية.** المسافة اللي يحقنها البرنامج تُقرأ كضغطة جديدة
  على زر التحول فيبدأ طور جديد — حلقة ترسل مسافة في نافذتك النشطة كل بضع ثوانٍ بلا توقف.
- **الإيقاف المؤقت بـ `F8` لا يوقف طوراً بدأ فعلاً.** يكمل حتى النهاية ويرسل المسافة، حتى لو الواجهة
  تقول `PAUSED`.
- **زر "تجربة · Test run" يتجاهل حالة الإيقاف** ويرسل مسافة حقيقية.
- **خانة "تحذير مبكر" صامتة إلا إذا كان التنبيه الصوتي (Beep) مفعّلاً.** النطق الصوتي لا يعلن التحذير أبداً.
- **ما فيه فلتر لنافذة اللعبة.** زر التحول يعمل من أي تطبيق، والمسافة تذهب للنافذة النشطة وقت الإرسال.
  اضغط `F8` قبل ما تكتب في الشات أو المتصفح.
- **تغيير المدة لا يحرّك لحظة القفزة معها.** راجعها يدوياً بعد أي تعديل للمدة.
- البرنامج يستهلك جزءاً ملحوظاً من نواة المعالج حتى وهو واقف بلا نشاط.
- بعض الألعاب تتجاهل الضغطات المحقونة عبر `SendInput`، وبعض أنظمة مكافحة الغش قد ترصدها. تأكد أن استخدام
  أدوات كهذه مسموح في اللعبة اللي تلعبها.

### البناء من المصدر

هذا المستودع يحتوي `ChibiPulse.cs` فقط. لبنائه تحتاج كذلك ملف `app.manifest` يعلن `PerMonitorV2` للـ DPI
و `asInvoker` للصلاحيات، ولازم تمرر `/codepage:65001` للمصرّف — المصدر مكتوب UTF-8 **بدون BOM** وفيه نصوص
عربية، وبدون هذه الراية يقرأها `csc.exe` بكودبيج النظام وتنطبع مشوّهة داخل البرنامج.

```bat
csc /target:winexe /optimize+ /platform:anycpu /codepage:65001 ^
    /win32manifest:app.manifest ^
    /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll ^
    /out:ChibiPulse.exe ChibiPulse.cs
```

المصرّف مرفق مع ويندوز في `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`، فما يحتاج تنصيب
Visual Studio.

### الرخصة

MIT

</div>
