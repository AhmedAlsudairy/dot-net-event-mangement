namespace GuestEvents.Services;

/// <summary>Short "how this page works" texts shown in the collapsible guide panel on each page (English / Arabic).</summary>
public static class GuideContent
{
    public record Guide(string Title, string[] Steps);

    private static readonly Dictionary<string, (Guide en, Guide ar)> G = new()
    {
        ["events"] = (
            new("Your events at a glance", new[] {
                "Press **New event request** to ask for an external guest presenter.",
                "Use the tiles and the search box to find events by status, presenter or venue.",
                "Click a row to open an event. After approval you can download the image and QR code and send a food request." }),
            new("فعالياتك في لمحة", new[] {
                "اضغط **طلب فعالية جديد** لطلب مقدّم ضيف من خارج الشركة.",
                "استخدم المربعات وخانة البحث للوصول إلى الفعاليات حسب الحالة أو المقدّم أو المكان.",
                "اضغط على أي صف لفتح الفعالية. بعد الموافقة يمكنك تحميل الصورة ورمز QR وإرسال طلب الطعام." })),
        ["create"] = (
            new("Filling in the request", new[] {
                "Fields marked **\\*** are required to submit. You can **Save draft** at any time and finish later.",
                "Choose **One day** or **Multiple days**. For several days pick the first and last date - the start and end time then apply to every day.",
                "Company name, logo, presenter, dates, venue and content are printed on the event image automatically after approval.",
                "Write the content in **English and Arabic** - the image is bilingual. Keep it short (a few lines).",
                "Press **Submit for approval**. The HOD is notified and you get an email with the decision." }),
            new("تعبئة الطلب", new[] {
                "الحقول المعلّمة بـ **\\*** مطلوبة عند الإرسال. يمكنك **حفظ كمسودة** في أي وقت وإكمالها لاحقاً.",
                "اختر **يوماً واحداً** أو **عدة أيام**. لعدة أيام حدّد أول وآخر تاريخ - ووقت البدء والانتهاء ينطبقان على كل يوم.",
                "اسم الشركة والشعار والمقدّم والتواريخ والمكان والمحتوى تُطبع على صورة الفعالية تلقائياً بعد الموافقة.",
                "اكتب المحتوى **بالعربية والإنجليزية** - الصورة ثنائية اللغة. اجعله قصيراً (بضعة أسطر).",
                "اضغط **إرسال للموافقة**. سيصل إشعار لرئيس القسم وتصلك رسالة بالقرار." })),
        ["approvals"] = (
            new("Reviewing requests", new[] {
                "Check the facts on each card. Click the event name to see everything, including the content text.",
                "**Approve** does everything else automatically: image, QR code and emails to Media, the company contact, Admin and the requester.",
                "**Reject** needs a comment. The requester can fix the request and send it again - you will see it here again.",
                "If a request waits too long (default 2 working days, Sunday to Thursday) a reminder email is sent." }),
            new("مراجعة الطلبات", new[] {
                "راجع المعلومات في كل بطاقة. اضغط على اسم الفعالية لعرض كل التفاصيل بما فيها نص المحتوى.",
                "**الموافقة** تنفّذ كل شيء تلقائياً: الصورة ورمز QR ورسائل البريد إلى الإعلام وجهة الاتصال والإدارة ومقدّم الطلب.",
                "**الرفض** يتطلب تعليقاً. يمكن لمقدّم الطلب التعديل وإعادة الإرسال وسيظهر لك هنا مرة أخرى.",
                "إذا طال انتظار الطلب (افتراضياً يومان عمل، من الأحد إلى الخميس) تُرسل رسالة تذكير." })),
        ["food-create"] = (
            new("Ordering food for the event", new[] {
                "Only **approved** events can have a food request.",
                "Enter the number of people, meal type, delivery time and place. Add allergies or special instructions in the notes.",
                "The request is emailed to the admin mailbox. You will be told when it is **Confirmed** or **Rejected**." }),
            new("طلب الطعام للفعالية", new[] {
                "طلب الطعام متاح فقط للفعاليات **المعتمدة**.",
                "أدخل عدد الأشخاص ونوع الوجبة ووقت التسليم والمكان. أضف الحساسية أو التعليمات الخاصة في الملاحظات.",
                "يُرسل الطلب إلى بريد الإدارة وسيتم إشعارك عند **التأكيد** أو **الرفض**." })),
        ["food-admin"] = (
            new("Food requests", new[] {
                "Each row is a request from a requester for an approved event.",
                "Press the green tick to **confirm** or the red cross to **reject**.",
                "The requester receives an email with your decision." }),
            new("طلبات الطعام", new[] {
                "كل صف هو طلب من مقدّم طلب لفعالية معتمدة.",
                "اضغط العلامة الخضراء **للتأكيد** أو العلامة الحمراء **للرفض**.",
                "يصل لمقدّم الطلب بريد إلكتروني بقرارك." })),
        ["reports"] = (
            new("Reading the reports", new[] {
                "Each row is an approved event: people who checked in versus expected, evaluations received and the average score.",
                "Click an event for the attendance list, charts and Excel export.",
                "**Response rate** = evaluations / people checked in. The target is above 60%." }),
            new("قراءة التقارير", new[] {
                "كل صف هو فعالية معتمدة: عدد من سجّلوا حضورهم مقابل المتوقع والتقييمات المستلمة والمتوسط.",
                "اضغط على الفعالية لعرض قائمة الحضور والرسوم البيانية وتصدير Excel.",
                "**نسبة الاستجابة** = التقييمات ÷ من سجّلوا الحضور. الهدف أعلى من 60٪." })),
        ["report-event"] = (
            new("Event report", new[] {
                "The counters refresh every 15 seconds, so you can watch check-in live during the event.",
                "The attendance list shows names. Evaluations are **anonymous** and cannot be linked to a name.",
                "Use the Excel buttons to export the attendance list or the evaluations." }),
            new("تقرير الفعالية", new[] {
                "تتحدّث العدّادات كل 15 ثانية لتتابع تسجيل الحضور مباشرة أثناء الفعالية.",
                "قائمة الحضور تعرض الأسماء، أما التقييمات فهي **مجهولة** ولا يمكن ربطها بالأسماء.",
                "استخدم أزرار Excel لتصدير قائمة الحضور أو التقييمات." })),
        ["settings"] = (
            new("How settings work", new[] {
                "Use the tabs on the left. **One Save button saves every tab.**",
                "Changes apply immediately to new events and new emails; past events are not changed.",
                "Email templates can use placeholders such as {event_name}, {date} and {venue} - they are replaced when the email is sent.",
                "Sign-in and email servers are on the **LDAP & Email** page; people and roles are on the **Roles** page." }),
            new("طريقة عمل الإعدادات", new[] {
                "استخدم التبويبات على اليسار. **زر حفظ واحد يحفظ كل التبويبات.**",
                "التغييرات تسري فوراً على الفعاليات والرسائل الجديدة ولا تغيّر الفعاليات السابقة.",
                "يمكن استخدام عناصر مثل {event_name} و{date} و{venue} في قوالب البريد وتُستبدل عند الإرسال.",
                "خوادم الدخول والبريد في صفحة **LDAP والبريد**، والأشخاص والأدوار في صفحة **الأدوار**." })),
        ["connections"] = (
            new("Connecting to Active Directory and email", new[] {
                "**1.** Enter the server details and press **Test**. Testing never saves anything.",
                "**2.** In the LDAP box add a test username and password to try a full sign-in - it shows the roles that person would get.",
                "**3.** Press **Save** when the tests are green. Saved values replace appsettings.json.",
                "Passwords are encrypted and never shown again. Leave a password box empty to keep the saved one." }),
            new("الربط مع Active Directory والبريد", new[] {
                "**1.** أدخل بيانات الخادم واضغط **اختبار**. الاختبار لا يحفظ أي شيء.",
                "**2.** في قسم LDAP أضف اسم مستخدم وكلمة مرور للتجربة - ستظهر الأدوار التي سيحصل عليها.",
                "**3.** اضغط **حفظ** عندما تنجح الاختبارات. القيم المحفوظة تحل محل appsettings.json.",
                "كلمات المرور مشفّرة ولا تُعرض مرة أخرى. اترك الخانة فارغة للإبقاء على المحفوظة." })),
        ["roles"] = (
            new("Giving people roles", new[] {
                "People who are in the AD groups already have a role - you do not need to add them here.",
                "Use this page when an AD group does not exist yet, or for one-off people (e.g. a deputy).",
                "They sign in with their normal password. The new role applies from their **next sign-in**." }),
            new("منح الأدوار للأشخاص", new[] {
                "الأشخاص الموجودون في مجموعات AD لديهم دور بالفعل - لا حاجة لإضافتهم هنا.",
                "استخدم هذه الصفحة عندما لا تكون مجموعة AD موجودة بعد، أو لأشخاص بعينهم (مثل نائب).",
                "يسجّلون الدخول بكلمة مرورهم المعتادة، ويسري الدور الجديد من **تسجيل الدخول التالي**." })),
        ["audit"] = (
            new("Audit log", new[] {
                "Every sign-in, approval, rejection, setting change and export is recorded with who did it and when.",
                "Use it to answer \"who changed this?\". Entries cannot be edited." }),
            new("سجل التدقيق", new[] {
                "يُسجَّل كل دخول وموافقة ورفض وتغيير في الإعدادات وتصدير مع اسم المنفّذ والوقت.",
                "استخدمه للإجابة عن سؤال \"من غيّر هذا؟\". لا يمكن تعديل السجلات." })),
        ["notifications"] = (
            new("Email log", new[] {
                "Every automatic email is listed with its result.",
                "A red cross shows the reason - for example email sending is switched off or the server refused the login. Fix it on the **LDAP & Email** page." }),
            new("سجل الرسائل", new[] {
                "تُعرض كل رسالة تلقائية مع نتيجتها.",
                "تظهر العلامة الحمراء مع السبب - مثلاً إرسال البريد متوقف أو رفض الخادم الدخول. عالج ذلك من صفحة **LDAP والبريد**." })),
        ["screen"] = (
            new("Venue screen", new[] {
                "Show this page on the projector so attendees can scan the QR code with their phone camera.",
                "With the rotating QR the code changes every minute, so a photo of it cannot be shared later." }),
            new("شاشة القاعة", new[] {
                "اعرض هذه الصفحة على جهاز العرض ليمسح الحضور الرمز بكاميرا هواتفهم.",
                "مع الرمز المتغيّر يتجدّد الرمز كل دقيقة فلا يمكن مشاركة صورته لاحقاً." })),
    };

    public static Guide? Get(string key, bool ar) => G.TryGetValue(key, out var g) ? (ar ? g.ar : g.en) : null;
}
