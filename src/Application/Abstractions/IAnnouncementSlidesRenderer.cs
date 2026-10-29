using ChurchAttendance.Application.Announcements;

namespace ChurchAttendance.Application.Abstractions;

// Lays out Sunday's announcements as a PowerPoint deck to project, on the church's own template
// (4:3, Calibri, banner on the title slide), like the one the secretariat used to make by hand.
public interface IAnnouncementSlidesRenderer
{
    byte[] Render(AnnouncementDeck deck);
}
