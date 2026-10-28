using ChurchAttendance.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;

namespace ChurchAttendance.Infrastructure.Cards;

// The card font ships in wwwroot/fonts, hence IWebHostEnvironment (see CardImageService).
internal sealed class MemberCardRenderer(IWebHostEnvironment env) : IMemberCardRenderer
{
    public byte[] RenderZip(IEnumerable<(string FullName, string CardUrl)> members) =>
        MemberCardZipService.BuildZip(members, env.WebRootPath);
}
