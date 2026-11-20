using System.ComponentModel.DataAnnotations;

namespace GuestEvents.Models;

public static class Roles
{
    public const string Staff = "Staff", Hod = "HOD", Dean = "Dean", Media = "Media", Admin = "Admin", Research = "Research";
    /// <summary>KPI team: works only in the KPI pages (no events, visits or research). Its KPI permissions are the KPI role of the same name.</summary>
    public const string Kpi = "Kpi";
    /// <summary>The finer KPI roles. Each one is also the name of a KPI role that holds its permissions.</summary>
    public const string KpiManager = "KpiManager", KpiEditor = "KpiEditor", KpiOfficer = "KpiOfficer", KpiViewer = "KpiViewer";
    /// <summary>Everyone who works only in the KPI pages (none of these has any events-side access).</summary>
    public static readonly string[] KpiRoles = { Kpi, KpiManager, KpiEditor, KpiOfficer, KpiViewer };
}

/// <summary>Draft -> Submitted (going through the approval steps the Admin configured, one after the other) -> Approved, or Rejected at any step.
/// The numbers are stored in the database, so they must not change.</summary>
public enum EventStatus { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3, EventDone = 4, EvaluationClosed = 5 }

public static class EventStatuses
{
    /// <summary>The event has been fully approved (HOD and Dean) and is running or over.</summary>
    public static readonly EventStatus[] Live = { EventStatus.Approved, EventStatus.EventDone, EventStatus.EvaluationClosed };
    public static bool IsLive(this EventStatus s) => s is EventStatus.Approved or EventStatus.EventDone or EventStatus.EvaluationClosed;
}
public enum FoodStatus { Submitted, Confirmed, Rejected }
public enum MealType { Breakfast, Lunch, CoffeeBreak, Dinner }

public class EventRequest
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Type { get; set; } = "workshop"; // workshop, seminar, training, other
    [Required, StringLength(200)] public string Presenter { get; set; } = "";
    [Required, StringLength(200)] public string CompanyName { get; set; } = "";
    public string? CompanyLogoPath { get; set; }
    [Required, StringLength(200)] public string ContactName { get; set; } = "";
    [Required, EmailAddress, StringLength(200)] public string ContactEmail { get; set; } = "";
    [StringLength(50)] public string? ContactPhone { get; set; }
    [DataType(DataType.Date)] public DateTime Date { get; set; } = DateTime.Today;
    [DataType(DataType.Time)] public TimeSpan StartTime { get; set; } = new(9, 0, 0);
    [DataType(DataType.Time)] public TimeSpan EndTime { get; set; } = new(11, 0, 0);
    [Required, StringLength(200)] public string Venue { get; set; } = "";
    [Range(1, 100000)] public int ExpectedAttendees { get; set; } = 20;
    public string? ContentAr { get; set; }
    public string? ContentEn { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public string RequesterUsername { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string? RequesterDepartment { get; set; }
    public string? RequesterEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }

    /// <summary>While Submitted: the approval step the request is waiting at, and since when.</summary>
    public int? CurrentStepId { get; set; }
    public DateTime? StepStartedAt { get; set; }

    /// <summary>The KPI (from the KPI dashboard) this event helps. KpiLabel is a snapshot of its number and text so the page still reads well if the KPI list changes.</summary>
    public int? KpiId { get; set; }
    [StringLength(400)] public string? KpiLabel { get; set; }

    /// <summary>Last day of a multi-day event (null = one-day event). The start/end times apply to EVERY day.</summary>
    [DataType(DataType.Date)] public DateTime? EndDate { get; set; }
    public DateTime LastDate => (EndDate ?? Date).Date;
    public bool IsMultiDay => LastDate > Date.Date;
    public int DayCount => (LastDate - Date.Date).Days + 1;
    public DateTime Start => Date.Date + StartTime;
    public DateTime End => LastDate + EndTime;

    public List<Approval> Approvals { get; set; } = new();
    public EventImage? Image { get; set; }
    public QRCodeInfo? QR { get; set; }
    public List<FoodRequest> FoodRequests { get; set; } = new();
}

/// <summary>One step of the event approval chain. Steps run in Order; a step is done when RequiredApprovals different eligible people approved it.</summary>
public class ApprovalStep
{
    public int Id { get; set; }
    /// <summary>Position in the chain (1 = first). Steps run from the lowest to the highest.</summary>
    public int Order { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    /// <summary>Anyone holding this role may decide (used only when Approvers is empty). One of Roles.*; empty = nobody by role.</summary>
    [StringLength(20)] public string? Role { get; set; }
    /// <summary>Comma separated emails or usernames. When filled in, ONLY these people may decide at this step.</summary>
    [StringLength(1000)] public string? Approvers { get; set; }
    /// <summary>How many different people must approve before the request moves on (a rejection by any eligible person rejects it).</summary>
    [Range(1, 20)] public int RequiredApprovals { get; set; } = 1;
    /// <summary>Extra address(es) told when a request arrives at this step. Emails in Approvers are always told too.</summary>
    [StringLength(500)] public string? NotifyEmail { get; set; }
    public bool Enabled { get; set; } = true;
}

public class Approval
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    public EventRequest? EventRequest { get; set; }
    public string ApproverUsername { get; set; } = "";
    public string ApproverName { get; set; } = "";
    public bool Approved { get; set; }
    /// <summary>The approval step this decision was made at (a snapshot of its name is kept, so history survives edits).</summary>
    public int? StepId { get; set; }
    public string? StepName { get; set; }
    public string? Comment { get; set; }
    public DateTime DecidedAt { get; set; } = DateTime.UtcNow;
}

public class EventImage
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    public string FilePath { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class QRCodeInfo
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    public EventRequest? EventRequest { get; set; }
    [StringLength(64)] public string Token { get; set; } = "";
    public DateTime CheckInOpen { get; set; }
    public DateTime CheckInClose { get; set; }
    public DateTime EvaluationClose { get; set; }
}

public class Attendance
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [Required, StringLength(200)] public string Organization { get; set; } = "";
    [StringLength(200)] public string? Email { get; set; }
    [StringLength(50)] public string? Phone { get; set; }
    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    /// <summary>Which event day this check-in is for (one check-in per device per day).</summary>
    public DateTime Day { get; set; } = DateTime.Today;
    [StringLength(100)] public string DeviceHash { get; set; } = ""; // salt A, attendance only
}

/// <summary>Deliberately has no reference to Attendance and uses a different device-hash salt.</summary>
public class Evaluation
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    public int Overall { get; set; }
    public int Presenter { get; set; }
    public int Content { get; set; }
    public int VenueOrg { get; set; }
    public int? Food { get; set; }
    public string? Comments { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    [StringLength(100)] public string DeviceHash { get; set; } = ""; // salt B, evaluation only
}

public class FoodRequest
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    public EventRequest? EventRequest { get; set; }
    [Range(1, 100000)] public int People { get; set; }
    public MealType Meal { get; set; }
    [DataType(DataType.Time)] public TimeSpan DeliveryTime { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = "";
    public string? Notes { get; set; }
    public FoodStatus Status { get; set; } = FoodStatus.Submitted;
    public string RequestedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Draft -> Submitted (waits for the HOD) -> Approved (waits for the bus) -> BusConfirmed, or Rejected.</summary>
public enum VisitStatus { Draft, Submitted, Approved, Rejected, BusConfirmed }

/// <summary>An external (off-campus) student visit for which staff request a bus.</summary>
public class ExternalVisit
{
    public int Id { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, StringLength(200)] public string Destination { get; set; } = "";
    [StringLength(500)] public string? DestinationAddress { get; set; }
    public string? Purpose { get; set; }
    [DataType(DataType.Date)] public DateTime Date { get; set; } = DateTime.Today;
    [DataType(DataType.Time)] public TimeSpan DepartureTime { get; set; } = new(8, 0, 0);
    [DataType(DataType.Time)] public TimeSpan ReturnTime { get; set; } = new(13, 0, 0);
    [Required, StringLength(200)] public string PickupLocation { get; set; } = "";
    [StringLength(100)] public string? Grade { get; set; }
    [Range(1, 1000)] public int StudentCount { get; set; } = 20;
    [Range(1, 100)] public int StaffCount { get; set; } = 2;
    [Required, StringLength(200)] public string SupervisorName { get; set; } = "";
    [Required, StringLength(50)] public string SupervisorPhone { get; set; } = "";
    public string? Notes { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Draft;
    public string RequesterUsername { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public string? RequesterDepartment { get; set; }
    public string? RequesterEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public string? DecisionBy { get; set; }
    public string? DecisionComment { get; set; }
    public DateTime? DecidedAt { get; set; }
    /// <summary>Filled by the admin when the bus is confirmed (bus number, driver, ...).</summary>
    public string? BusDetails { get; set; }

    /// <summary>The KPI (from the KPI dashboard) this visit helps, with a snapshot of its label.</summary>
    public int? KpiId { get; set; }
    [StringLength(400)] public string? KpiLabel { get; set; }
    public DateTime Departure => Date.Date + DepartureTime;
}

/// <summary>A research paper or proposal uploaded by staff. It is emailed to the research in-charge and kept in the archive; there is no approval step.</summary>
public class ResearchDoc
{
    public int Id { get; set; }
    [Required, StringLength(300)] public string Title { get; set; } = "";
    [Required, StringLength(50)] public string DocType { get; set; } = "research"; // research, proposal
    [StringLength(300)] public string? Authors { get; set; }
    public string? Summary { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public string UploaderUsername { get; set; } = "";
    public string UploaderName { get; set; } = "";
    public string? UploaderDepartment { get; set; }
    public string? UploaderEmail { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool EmailSent { get; set; }
}

public class AppSetting
{
    [Key, StringLength(100)] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public class Notification
{
    public int Id { get; set; }
    public int? EventRequestId { get; set; }
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>Role given inside the app to an AD user (added to whatever the user gets from AD groups).</summary>
public class RoleAssignment
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Username { get; set; } = "";
    [Required, StringLength(20)] public string Role { get; set; } = "";
    public string AssignedBy { get; set; } = "";
    /// <summary>Only for the KpiOfficer role: the KPI department (entity) this person updates.</summary>
    public int? EntityId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One line of the audit / activity log. Entries are only ever added (and removed by the retention clean-up), never edited.</summary>
public class AuditLog
{
    public int Id { get; set; }
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    /// <summary>Auth, Events, Visits, Research, Approvals, Admin, Access, KPI, Data export, System.</summary>
    [StringLength(30)] public string Category { get; set; } = "General";
    /// <summary>Info, Warning, Error or Security.</summary>
    [StringLength(10)] public string Severity { get; set; } = "Info";
    /// <summary>Success or Failure.</summary>
    [StringLength(10)] public string Outcome { get; set; } = "Success";
    [StringLength(60)] public string? Ip { get; set; }
    [StringLength(250)] public string? UserAgent { get; set; }
    [StringLength(10)] public string? Method { get; set; }
    [StringLength(300)] public string? Path { get; set; }
    /// <summary>What the action was about, for example "Event:12" or "Visit:3".</summary>
    [StringLength(80)] public string? Target { get; set; }
    /// <summary>The roles the person had when it happened.</summary>
    [StringLength(200)] public string? Roles { get; set; }
    /// <summary>Same value for every entry written during one web request.</summary>
    [StringLength(40)] public string? CorrelationId { get; set; }
    public int? DurationMs { get; set; }
}
