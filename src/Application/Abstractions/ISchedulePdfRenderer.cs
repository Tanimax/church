using ChurchAttendance.Application.Schedules;

namespace ChurchAttendance.Application.Abstractions;

// Lays out the quarterly "Service dominical" sheet as a one-page PDF (Letter), like the one the
// church used to make by hand: banner, title, period, one row per Sunday, N.B. line.
public interface ISchedulePdfRenderer
{
    byte[] Render(ScheduleSheet sheet);
}
