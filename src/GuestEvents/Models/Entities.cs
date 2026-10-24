using System.ComponentModel.DataAnnotations;

namespace GuestEvents.Models;

public static class Roles
{
    public const string Staff = "Staff", Hod = "HOD", Media = "Media", Admin = "Admin", Research = "Research";
}

public enum EventStatus { Draft, Submitted, Approved, Rejected, EventDone, EvaluationClosed }
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

public class Approval
{
    public int Id { get; set; }
    public int EventRequestId { get; set; }
    public EventRequest? EventRequest { get; set; }
    public string ApproverUsername { get; set; } = "";
    public string ApproverName { get; set; } = "";
    public bool Approved { get; set; }
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
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
