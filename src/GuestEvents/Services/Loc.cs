using System.Globalization;

namespace GuestEvents.Services;

/// <summary>Minimal Arabic/English dictionary. Language is stored in the "lang" cookie.</summary>
public class Loc
{
    private readonly IHttpContextAccessor _h;
    public Loc(IHttpContextAccessor h) => _h = h;
    public bool IsAr => (_h.HttpContext?.Request.Cookies["lang"] ?? "en") == "ar";
    public string Dir => IsAr ? "rtl" : "ltr";
    public string Lang => IsAr ? "ar" : "en";
    public string this[string key] => D.TryGetValue(key, out var v) ? (IsAr ? v.ar : v.en) : key;

    /// <summary>Inline bilingual text: T("English", "عربي").</summary>
    public string T(string en, string ar) => IsAr ? ar : en;

    public static bool UseArabicDigits { get; set; }
    public static string DateFmt { get; set; } = "yyyy-MM-dd";

    public string Num(object? n)
    {
        var s = Convert.ToString(n, CultureInfo.InvariantCulture) ?? "";
        if (!UseArabicDigits) return s;
        return new string(s.Select(c => c is >= '0' and <= '9' ? (char)('٠' + (c - '0')) : c).ToArray());
    }
    /// <summary>"2026-10-05" for a one-day event, "2026-10-05 - 2026-10-07" for a multi-day one.</summary>
    public string Range(GuestEvents.Models.EventRequest e) => e.IsMultiDay ? Date(e.Date) + " - " + Date(e.LastDate) : Date(e.Date);
    /// <summary>"09:00 - 11:00", with "daily" in front for multi-day events.</summary>
    public string Times(GuestEvents.Models.EventRequest e) => (e.IsMultiDay ? T("Daily ", "يومياً ") : "") + Time(e.StartTime) + " - " + Time(e.EndTime);
    public string Date(DateTime d) => Num(d.ToString(DateFmt, CultureInfo.InvariantCulture));
    public string Time(TimeSpan t) => Num(t.ToString(@"hh\:mm"));
    public string DateTimeStr(DateTime d) => Num(d.ToString(DateFmt + " HH:mm", CultureInfo.InvariantCulture));

    private static readonly Dictionary<string, (string en, string ar)> D = new()
    {
        ["App"] = ("Guest Events", "فعاليات الضيوف"), ["Login"] = ("Sign in", "تسجيل الدخول"), ["Logout"] = ("Sign out", "تسجيل الخروج"),
        ["Username"] = ("Username", "اسم المستخدم"), ["Password"] = ("Password", "كلمة المرور"), ["BadLogin"] = ("Invalid credentials or no access.", "بيانات غير صحيحة أو لا توجد صلاحية."),
        ["MyEvents"] = ("My events", "فعالياتي"), ["NewEvent"] = ("New event request", "طلب فعالية جديد"), ["Pending"] = ("Pending approvals", "بانتظار الموافقة"),
        ["Admin"] = ("Admin", "الإدارة"), ["Reports"] = ("Reports", "التقارير"), ["Settings"] = ("Settings", "الإعدادات"), ["Audit"] = ("Audit log", "سجل التدقيق"),
        ["Name"] = ("Event name", "اسم الفعالية"), ["Type"] = ("Type", "النوع"), ["Presenter"] = ("Presenter", "المقدم"), ["Company"] = ("Company name", "اسم الشركة"),
        ["Logo"] = ("Company logo (PNG/JPG)", "شعار الشركة (PNG/JPG)"), ["ContactName"] = ("Contact name", "اسم جهة الاتصال"), ["ContactEmail"] = ("Contact email", "بريد جهة الاتصال"),
        ["ContactPhone"] = ("Contact phone", "هاتف جهة الاتصال"), ["Date"] = ("Date", "التاريخ"), ["Start"] = ("Start time", "وقت البدء"), ["End"] = ("End time", "وقت الانتهاء"),
        ["Venue"] = ("Venue", "المكان"), ["Expected"] = ("Expected attendees", "عدد الحضور المتوقع"), ["ContentAr"] = ("Event content (Arabic)", "محتوى الفعالية (عربي)"),
        ["ContentEn"] = ("Event content (English)", "محتوى الفعالية (إنجليزي)"), ["SaveDraft"] = ("Save draft", "حفظ كمسودة"), ["Submit"] = ("Submit for approval", "إرسال للموافقة"),
        ["Save"] = ("Save", "حفظ"), ["Status"] = ("Status", "الحالة"), ["Requester"] = ("Requester", "مقدم الطلب"), ["Department"] = ("Department", "القسم"),
        ["Approve"] = ("Approve", "موافقة"), ["Reject"] = ("Reject", "رفض"), ["Comment"] = ("Comment", "تعليق"), ["CommentRequired"] = ("A comment is required when rejecting.", "التعليق مطلوب عند الرفض."),
        ["History"] = ("Decision history", "سجل القرارات"), ["Edit"] = ("Edit", "تعديل"), ["Resubmit"] = ("Edit & resubmit", "تعديل وإعادة الإرسال"), ["Details"] = ("Details", "التفاصيل"),
        ["DownloadImage"] = ("Download image", "تحميل الصورة"), ["DownloadQr"] = ("Download QR", "تحميل رمز QR"), ["FoodRequest"] = ("Food request", "طلب طعام"),
        ["People"] = ("Number of people", "عدد الأشخاص"), ["Meal"] = ("Meal type", "نوع الوجبة"), ["DeliveryTime"] = ("Delivery time", "وقت التسليم"), ["Location"] = ("Location", "الموقع"),
        ["Notes"] = ("Dietary notes / instructions", "ملاحظات غذائية / تعليمات"), ["Confirm"] = ("Confirm", "تأكيد"), ["None"] = ("Nothing here yet.", "لا يوجد شيء بعد."),
        ["Actions"] = ("Actions", "إجراءات"), ["Open"] = ("Open", "فتح"), ["Back"] = ("Back", "رجوع"), ["By"] = ("By", "بواسطة"), ["At"] = ("At", "في"),
        ["Draft"] = ("Draft", "مسودة"), ["Submitted"] = ("Submitted", "مرسل"), ["Approved"] = ("Approved", "معتمد"), ["Rejected"] = ("Rejected", "مرفوض"),
        ["EventDone"] = ("Event done", "انتهت الفعالية"), ["EvaluationClosed"] = ("Evaluation closed", "أُغلق التقييم"), ["Confirmed"] = ("Confirmed", "مؤكد"),
        ["Breakfast"] = ("Breakfast", "فطور"), ["Lunch"] = ("Lunch", "غداء"), ["CoffeeBreak"] = ("Coffee break", "استراحة قهوة"), ["Dinner"] = ("Dinner", "عشاء"),
        ["workshop"] = ("Workshop", "ورشة عمل"), ["seminar"] = ("Seminar", "ندوة"), ["training"] = ("Training", "تدريب"), ["other"] = ("Other", "أخرى"),
        ["CheckIn"] = ("Check in", "تسجيل الحضور"), ["FullName"] = ("Full name", "الاسم الكامل"), ["Org"] = ("Organization / department", "الجهة / القسم"),
        ["Email"] = ("Email", "البريد الإلكتروني"), ["Phone"] = ("Phone", "الهاتف"), ["Optional"] = ("optional", "اختياري"),
        ["CheckedIn"] = ("You are checked in. Thank you!", "تم تسجيل حضورك. شكراً لك!"), ["AlreadyCheckedIn"] = ("Already checked in", "تم تسجيل الحضور مسبقاً"),
        ["CheckInNotOpen"] = ("Check-in is not open yet.", "تسجيل الحضور لم يفتح بعد."), ["CheckInClosed"] = ("Check-in is closed.", "تسجيل الحضور مغلق."),
        ["LinkInvalid"] = ("This link is invalid or has expired.", "هذا الرابط غير صالح أو منتهي."), ["Evaluate"] = ("Evaluate this event", "قيّم هذه الفعالية"),
        ["EvalThanks"] = ("Thank you for your feedback!", "شكراً لملاحظاتك!"), ["EvalDone"] = ("You already submitted an evaluation.", "لقد أرسلت تقييماً مسبقاً."),
        ["EvalClosed"] = ("Evaluation is closed.", "التقييم مغلق."), ["MustCheckIn"] = ("Please check in first.", "يرجى تسجيل الحضور أولاً."),
        ["Comments"] = ("Comments", "ملاحظات"), ["OutsideVenue"] = ("Check-in is only available on the venue network.", "تسجيل الحضور متاح فقط على شبكة المكان."),
        ["Anonymous"] = ("Your evaluation is anonymous.", "تقييمك مجهول الهوية."), ["Rating"] = ("1 = poor, 5 = excellent", "١ = ضعيف، ٥ = ممتاز"),
        ["Overall"] = ("Overall rating", "التقييم العام"), ["Required"] = ("Please fill the required fields.", "يرجى تعبئة الحقول المطلوبة."),
        ["Attendees"] = ("Attendees", "الحضور"), ["Responses"] = ("Evaluations", "التقييمات"), ["ResponseRate"] = ("Response rate", "نسبة الاستجابة"),
        ["Average"] = ("Average", "المتوسط"), ["ExportExcel"] = ("Export Excel", "تصدير Excel"), ["ByPresenter"] = ("Average by presenter", "المتوسط حسب المقدم"),
        ["Live"] = ("Live attendance", "الحضور المباشر"), ["Screen"] = ("Venue screen (rotating QR)", "شاشة القاعة (رمز متغير)"),
        ["HowThisWorks"] = ("How does this page work?", "كيف تعمل هذه الصفحة؟"), ["GotIt"] = ("Got it", "فهمت"), ["FullGuide"] = ("Open the full guide", "فتح الدليل الكامل"), ["Help"] = ("Help", "المساعدة"), ["OneDay"] = ("One day", "يوم واحد"), ["MultiDay"] = ("Multiple days", "عدة أيام"), ["FromDate"] = ("From date", "من تاريخ"), ["ToDate"] = ("To date", "إلى تاريخ"),
        ["DailyHint"] = ("The start and end time apply to every day of the event.", "وقت البدء والانتهاء ينطبقان على كل يوم من أيام الفعالية."), ["DayWord"] = ("days", "أيام"),
        ["EventDay"] = ("Day", "اليوم"), ["LoginDisabled"] = ("This account is disabled. Please contact IT.", "هذا الحساب معطّل. يرجى التواصل مع قسم تقنية المعلومات."),
        ["LoginLocked"] = ("This account is locked because of too many wrong passwords. Wait a few minutes or contact IT.", "الحساب مقفل بسبب كثرة محاولات كلمة المرور الخاطئة. انتظر قليلاً أو تواصل مع تقنية المعلومات."),
        ["LoginPwExpired"] = ("Your password has expired or must be changed. Change it on your computer, then sign in again.", "انتهت صلاحية كلمة المرور أو يجب تغييرها. غيّرها من جهازك ثم سجّل الدخول مجدداً."),
        ["LoginAcctExpired"] = ("This account has expired. Please contact IT.", "انتهت صلاحية هذا الحساب. يرجى التواصل مع تقنية المعلومات."),
        ["LoginUnavailable"] = ("The company directory is not reachable right now. This is not a problem with your password - try again in a few minutes or contact IT.", "خادم الشركة غير متاح حالياً. المشكلة ليست في كلمة مرورك - حاول بعد قليل أو تواصل مع تقنية المعلومات."),
        ["LoginTooMany"] = ("Too many wrong attempts for this name. Please wait 10 minutes and try again.", "محاولات خاطئة كثيرة لهذا الاسم. يرجى الانتظار 10 دقائق ثم المحاولة مجدداً."),
        ["LoginNoRole"] = ("Your password is correct but you have no role in this system yet. Ask the Admin to add you.", "كلمة مرورك صحيحة لكن ليس لديك دور في هذا النظام بعد. اطلب من الإدارة إضافتك."),
        ["SubmitEval"] = ("Submit evaluation", "إرسال التقييم"), ["NewTag"] = ("New", "جديد"), ["SavedOk"] = ("Saved", "تم الحفظ"), ["All"] = ("All", "الكل"), ["Events"] = ("events", "فعاليات"), ["Search"] = ("Search events, presenters, venues...", "ابحث في الفعاليات والمقدمين والأماكن..."),
        ["NoResults"] = ("No events match your search.", "لا توجد فعاليات مطابقة."), ["EmptyHint"] = ("Request your first guest event to get started.", "ابدأ بطلب أول فعالية لضيف."),
        ["WaitingHod"] = ("Waiting for the HOD decision. You will get an email when it is approved.", "بانتظار قرار رئيس القسم. ستصلك رسالة عند الموافقة."),
        ["Content"] = ("Event content", "محتوى الفعالية"), ["PeopleShort"] = ("people", "شخص"),
        ["FormHint"] = ("Fill in the event details. You can save a draft and finish later.", "أدخل تفاصيل الفعالية. يمكنك الحفظ كمسودة والإكمال لاحقاً."),
        ["SecEvent"] = ("Event", "الفعالية"), ["SecCompany"] = ("Company & contact", "الشركة وجهة الاتصال"), ["SecWhen"] = ("When & where", "الوقت والمكان"), ["SecContent"] = ("Content for the event image", "المحتوى لصورة الفعالية"),
        ["LogoHint"] = ("PNG or JPG, up to 2 MB. Printed on the event image.", "PNG أو JPG حتى 2 ميجابايت. تُطبع على صورة الفعالية."), ["LogoKeep"] = ("A logo is already saved. Choose a file only to replace it.", "الشعار محفوظ. اختر ملفاً فقط لاستبداله."),
        ["LogoTooBig"] = ("File is larger than 2 MB.", "الملف أكبر من 2 ميجابايت."), ["ContentHint"] = ("Printed on the event image in both languages. Keep it short.", "تُطبع على صورة الفعالية باللغتين. يُفضّل الاختصار."),
        ["SubmitHint"] = ("A draft can be incomplete; submitting needs all required fields.", "المسودة قد تكون ناقصة؛ الإرسال يتطلب تعبئة الحقول المطلوبة."),
        ["WaitingDecision"] = ("waiting for a decision", "بانتظار القرار"), ["NotAssignedHod"] = ("Only the assigned HOD can decide on requests.", "فقط رئيس القسم المعيّن يمكنه اتخاذ القرار."),
        ["AllCaughtUp"] = ("All caught up", "لا شيء معلّق"), ["NoPending"] = ("There are no requests waiting for your decision.", "لا توجد طلبات بانتظار قرارك."),
        ["WorkingDaysWaiting"] = ("working day(s) waiting", "يوم عمل بالانتظار"), ["RejectNeeds"] = ("required to reject", "مطلوب عند الرفض"),
        ["LoginTagline"] = ("Guest workshops and events, from request to evaluation.", "ورش وفعاليات الضيوف، من الطلب حتى التقييم."),
        ["LoginHint"] = ("Use your company account.", "استخدم حساب الشركة."),
        ["Feat1"] = ("One approval starts everything", "موافقة واحدة تبدأ كل الإجراءات"), ["Feat2"] = ("Event image created automatically", "صورة الفعالية تُنشأ تلقائياً"), ["Feat3"] = ("QR check-in and evaluation", "تسجيل الحضور والتقييم برمز QR"),
        ["EvalInvite"] = ("After the event, please rate it - it takes one minute.", "بعد الفعالية يرجى تقييمها - يستغرق دقيقة واحدة."), ["OpensAt"] = ("Opens at", "يفتح في"),
        ["Poor"] = ("Poor", "ضعيف"), ["Excellent"] = ("Excellent", "ممتاز"),
        ["FoodHint"] = ("Sent to the admin mailbox. You will be told when it is confirmed.", "يُرسل إلى بريد الإدارة وسيتم إشعارك عند التأكيد."), ["NotesHint"] = ("Allergies, vegetarian, halal, special instructions...", "حساسية، نباتي، حلال، تعليمات خاصة..."),
        ["AwaitingConfirm"] = ("awaiting confirmation", "بانتظار التأكيد"), ["ScanToCheckIn"] = ("Scan with your phone to check in", "امسح الرمز بهاتفك لتسجيل الحضور"),
        ["Roles"] = ("Roles", "الأدوار"), ["Connections"] = ("LDAP & Email", "LDAP والبريد"), ["Event"] = ("Event", "الفعالية"), ["AttendanceList"] = ("Attendance list", "قائمة الحضور"), ["Time"] = ("Time", "الوقت"),
    };
}
