namespace ChurchAttendance.Application.Abstractions;

// Draws member cards (name + QR code of the card URL) as PNG images.
public interface IMemberCardRenderer
{
    // One PNG per member, zipped, for printing cards in bulk.
    byte[] RenderZip(IEnumerable<(string FullName, string CardUrl)> members);
}
